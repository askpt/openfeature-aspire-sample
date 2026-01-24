"""Le Mans Chatbot Service - FastAPI application using GitHub Models and OpenFeature."""

import os
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from openai import OpenAI
from openfeature import api
from openfeature.contrib.provider.ofrep import OFREPProvider
from openfeature.evaluation_context import EvaluationContext
from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.sdk.resources import Resource

from prompt_loader import load_prompt, render_messages, get_model_parameters

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Get configuration from environment
OFREP_ENDPOINT = os.environ.get("OFREP_ENDPOINT", "http://localhost:8016")
# GitHub Models connection from Aspire (uses format: {RESOURCE}_{PROPERTY})
GITHUB_MODELS_ENDPOINT = os.environ.get("CHAT_MODEL_URI", "https://models.github.ai/inference")
GITHUB_TOKEN = os.environ.get("CHAT_MODEL_KEY", "")
GITHUB_MODEL_NAME = os.environ.get("CHAT_MODEL_MODELNAME", "openai/gpt-4o")
OTLP_ENDPOINT = os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT", "")
SERVICE_NAME = os.environ.get("OTEL_SERVICE_NAME", "chat-service")
PROMPTS_DIR = Path(__file__).parent / "prompts"


def setup_telemetry():
    """Configure OpenTelemetry tracing."""
    if not OTLP_ENDPOINT:
        logger.warning("OTLP_ENDPOINT not set, telemetry disabled")
        return
    
    resource = Resource.create({"service.name": SERVICE_NAME})
    provider = TracerProvider(resource=resource)
    exporter = OTLPSpanExporter(endpoint=f"{OTLP_ENDPOINT}/v1/traces")
    provider.add_span_processor(BatchSpanProcessor(exporter))
    trace.set_tracer_provider(provider)
    logger.info(f"OpenTelemetry configured with endpoint: {OTLP_ENDPOINT}")


def setup_openfeature():
    """Configure OpenFeature with OFREP provider."""
    provider = OFREPProvider(base_url=OFREP_ENDPOINT)
    api.set_provider(provider)
    logger.info(f"OpenFeature configured with OFREP endpoint: {OFREP_ENDPOINT}")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager."""
    setup_telemetry()
    setup_openfeature()
    logger.info("Chat service started")
    yield
    logger.info("Chat service shutting down")


# Create FastAPI app
app = FastAPI(
    title="Le Mans Chatbot",
    description="A chatbot that answers questions about Le Mans 24 Hours racing",
    version="1.0.0",
    lifespan=lifespan
)

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Instrument FastAPI with OpenTelemetry
FastAPIInstrumentor.instrument_app(app)

# Create OpenAI client for GitHub Models
openai_client = OpenAI(
    base_url=GITHUB_MODELS_ENDPOINT,
    api_key=GITHUB_TOKEN
)


class ChatRequest(BaseModel):
    """Request model for chat endpoint."""
    message: str
    userId: str = "anonymous"


class ChatResponse(BaseModel):
    """Response model for chat endpoint."""
    response: str
    prompt_style: str


class HealthResponse(BaseModel):
    """Response model for health endpoint."""
    status: str


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint."""
    return HealthResponse(status="healthy")


@app.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    """Chat endpoint that uses GitHub Models with dynamic prompt selection."""
    tracer = trace.get_tracer(__name__)
    
    with tracer.start_as_current_span("chat_request") as span:
        client = api.get_client()
        
        # Create evaluation context with user ID
        eval_context = EvaluationContext(
            targeting_key=request.userId,
            attributes={"userId": request.userId}
        )
        span.set_attribute("user.id", request.userId)
        
        # Check if chatbot is enabled
        chatbot_enabled = client.get_boolean_value("enable-chatbot", True, eval_context)
        span.set_attribute("feature.enable_chatbot", chatbot_enabled)
        
        if not chatbot_enabled:
            raise HTTPException(
                status_code=503,
                detail="Chatbot is currently disabled"
            )
        
        # Get the prompt file to use
        prompt_file = client.get_string_value("prompt-file", "expert", eval_context)
        span.set_attribute("feature.prompt_file", prompt_file)
        logger.info(f"Using prompt file: {prompt_file} for user: {request.userId}")
        
        try:
            # Load and render the prompt
            with tracer.start_as_current_span("load_prompt"):
                prompt = load_prompt(prompt_file, str(PROMPTS_DIR))
                messages = render_messages(prompt, {"message": request.message})
                model_params = get_model_parameters(prompt)
            
            # Call GitHub Models
            with tracer.start_as_current_span("github_models_call") as model_span:
                model_span.set_attribute("model", GITHUB_MODEL_NAME)
                model_span.set_attribute("temperature", model_params.get("temperature", 0.7))
                
                response = openai_client.chat.completions.create(
                    model=GITHUB_MODEL_NAME,
                    messages=messages,
                    temperature=model_params.get("temperature", 0.7)
                )
                
                answer = response.choices[0].message.content
                model_span.set_attribute("response_length", len(answer))
            
            return ChatResponse(response=answer, prompt_style=prompt_file)
            
        except FileNotFoundError:
            logger.error(f"Prompt file not found: {prompt_file}")
            raise HTTPException(
                status_code=500,
                detail=f"Prompt file '{prompt_file}' not found"
            )
        except Exception as e:
            logger.error(f"Error calling GitHub Models: {e}")
            raise HTTPException(
                status_code=500,
                detail=f"Error processing chat request: {str(e)}"
            )


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
