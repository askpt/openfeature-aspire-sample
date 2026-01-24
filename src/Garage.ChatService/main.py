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
from opentelemetry import trace, metrics
from opentelemetry._logs import set_logger_provider
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
from opentelemetry.sdk.resources import Resource, SERVICE_NAME as RESOURCE_SERVICE_NAME
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor

from prompt_loader import load_prompt, render_messages, get_model_parameters

# Get configuration from environment
OFREP_ENDPOINT = os.environ.get("OFREP_ENDPOINT", "http://localhost:8016")
# GitHub Models connection from Aspire (uses format: {RESOURCE}_{PROPERTY})
GITHUB_MODELS_ENDPOINT = os.environ.get("CHAT_MODEL_URI", "https://models.github.ai/inference")
GITHUB_TOKEN = os.environ.get("CHAT_MODEL_KEY", "")
GITHUB_MODEL_NAME = os.environ.get("CHAT_MODEL_MODELNAME", "openai/gpt-4o")
# OpenTelemetry configuration from Aspire
OTLP_ENDPOINT = os.environ.get("OTEL_EXPORTER_OTLP_ENDPOINT", "")
SERVICE_NAME = os.environ.get("OTEL_SERVICE_NAME", "chat-service")
# CORS configuration - allow specific origins from environment, default to localhost dev servers
CORS_ORIGINS = os.environ.get("CORS_ALLOWED_ORIGINS", "http://localhost:5173,http://localhost:5174").split(",")
PROMPTS_DIR = Path(__file__).parent / "prompts"

# Metrics - initialized after setup_telemetry
chat_request_counter = None
chat_request_duration = None

# Configure basic logging first (will be enhanced by OTLP later)
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


def setup_telemetry():
    """Configure OpenTelemetry tracing, metrics, and logging using gRPC exporter."""
    global chat_request_counter, chat_request_duration
    
    # Create resource with service name
    resource = Resource.create({
        RESOURCE_SERVICE_NAME: SERVICE_NAME
    })
    
    if not OTLP_ENDPOINT:
        logger.warning("OTEL_EXPORTER_OTLP_ENDPOINT not set, using no-op telemetry")
        # Create metrics even without export endpoint for local tracking
        meter = metrics.get_meter(__name__)
        chat_request_counter = meter.create_counter(
            name="chat_requests_total",
            description="Total number of chat requests",
            unit="1"
        )
        chat_request_duration = meter.create_histogram(
            name="chat_request_duration_seconds",
            description="Duration of chat requests in seconds",
            unit="s"
        )
        return
    
    logger.info(f"Configuring OpenTelemetry with endpoint: {OTLP_ENDPOINT}")
    
    # Parse endpoint - Aspire provides HTTPS endpoint, we need to handle it
    endpoint = OTLP_ENDPOINT
    
    # For gRPC, we need just host:port without the scheme
    if endpoint.startswith("https://"):
        grpc_endpoint = endpoint.replace("https://", "")
        use_tls = True
    elif endpoint.startswith("http://"):
        grpc_endpoint = endpoint.replace("http://", "")
        use_tls = False
    else:
        grpc_endpoint = endpoint
        use_tls = False
    
    logger.info(f"OTLP gRPC endpoint: {grpc_endpoint}, TLS: {use_tls}")
    
    try:
        from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
        from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
        from opentelemetry.exporter.otlp.proto.grpc._log_exporter import OTLPLogExporter
        
        # Determine credentials for TLS
        credentials = None
        if use_tls:
            ca_cert_path = os.environ.get("SSL_CERT_FILE", "")
            if ca_cert_path and os.path.exists(ca_cert_path):
                logger.info(f"Using CA cert from SSL_CERT_FILE: {ca_cert_path}")
                with open(ca_cert_path, "rb") as f:
                    ca_cert = f.read()
                import grpc
                credentials = grpc.ssl_channel_credentials(root_certificates=ca_cert)
        
        # Setup tracing
        if use_tls and credentials:
            trace_exporter = OTLPSpanExporter(endpoint=grpc_endpoint, credentials=credentials)
        else:
            trace_exporter = OTLPSpanExporter(endpoint=grpc_endpoint, insecure=True)
        
        trace_provider = TracerProvider(resource=resource)
        trace_provider.add_span_processor(BatchSpanProcessor(trace_exporter))
        trace.set_tracer_provider(trace_provider)
        
        # Setup metrics
        if use_tls and credentials:
            metric_exporter = OTLPMetricExporter(endpoint=grpc_endpoint, credentials=credentials)
        else:
            metric_exporter = OTLPMetricExporter(endpoint=grpc_endpoint, insecure=True)
        
        metric_reader = PeriodicExportingMetricReader(metric_exporter, export_interval_millis=10000)
        meter_provider = MeterProvider(resource=resource, metric_readers=[metric_reader])
        metrics.set_meter_provider(meter_provider)
        
        # Setup logging export to OTLP
        if use_tls and credentials:
            log_exporter = OTLPLogExporter(endpoint=grpc_endpoint, credentials=credentials)
        else:
            log_exporter = OTLPLogExporter(endpoint=grpc_endpoint, insecure=True)
        
        logger_provider = LoggerProvider(resource=resource)
        logger_provider.add_log_record_processor(BatchLogRecordProcessor(log_exporter))
        set_logger_provider(logger_provider)
        
        # Add OTLP handler to root logger to export logs
        otlp_handler = LoggingHandler(level=logging.INFO, logger_provider=logger_provider)
        logging.getLogger().addHandler(otlp_handler)
        
        # Instrument logging to add trace context to logs
        LoggingInstrumentor().instrument(set_logging_format=True)
        
        # Create metrics
        meter = metrics.get_meter(__name__)
        chat_request_counter = meter.create_counter(
            name="chat_requests_total",
            description="Total number of chat requests",
            unit="1"
        )
        chat_request_duration = meter.create_histogram(
            name="chat_request_duration_seconds",
            description="Duration of chat requests in seconds",
            unit="s"
        )
        
        logger.info(f"OpenTelemetry configured successfully for service: {SERVICE_NAME}")
        
    except Exception as e:
        logger.error(f"Failed to configure OpenTelemetry: {e}")
        # Create fallback metrics
        meter = metrics.get_meter(__name__)
        chat_request_counter = meter.create_counter(
            name="chat_requests_total",
            description="Total number of chat requests",
            unit="1"
        )
        chat_request_duration = meter.create_histogram(
            name="chat_request_duration_seconds",
            description="Duration of chat requests in seconds",
            unit="s"
        )


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

# Add CORS middleware - use configurable origins for security
app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=False,
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
    import time
    start_time = time.time()
    
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
            # Record metric for disabled requests
            if chat_request_counter:
                chat_request_counter.add(1, {"status": "disabled", "prompt_style": "none"})
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
            
            # Record successful request metrics
            duration = time.time() - start_time
            if chat_request_counter:
                chat_request_counter.add(1, {"status": "success", "prompt_style": prompt_file})
            if chat_request_duration:
                chat_request_duration.record(duration, {"prompt_style": prompt_file})
            
            return ChatResponse(response=answer, prompt_style=prompt_file)
            
        except FileNotFoundError:
            logger.error(f"Prompt file not found: {prompt_file}")
            if chat_request_counter:
                chat_request_counter.add(1, {"status": "error", "prompt_style": prompt_file})
            raise HTTPException(
                status_code=500,
                detail=f"Prompt file '{prompt_file}' not found"
            )
        except Exception as e:
            logger.error(f"Error calling GitHub Models: {e}")
            if chat_request_counter:
                chat_request_counter.add(1, {"status": "error", "prompt_style": prompt_file})
            raise HTTPException(
                status_code=500,
                detail="Error processing chat request"
            )


if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("PORT", 8080))
    uvicorn.run(app, host="0.0.0.0", port=port)
