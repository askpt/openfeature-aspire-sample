import json
import gc

try:
    import usocket as socket
except ImportError:
    import socket

try:
    import ussl as ssl
except ImportError:
    import ssl


def _parse_url(url):
    if url.startswith("https://"):
        scheme, url, port = "https", url[8:], 443
    elif url.startswith("http://"):
        scheme, url, port = "http", url[7:], 80
    else:
        raise ValueError("URL must start with http:// or https://")

    slash = url.find("/")
    if slash >= 0:
        host, path = url[:slash], url[slash:]
    else:
        host, path = url, "/"

    colon = host.find(":")
    if colon >= 0:
        port = int(host[colon + 1:])
        host = host[:colon]

    return scheme, host, port, path


def _http_post(url, body_bytes, headers, timeout=10):
    scheme, host, port, path = _parse_url(url)

    addr = socket.getaddrinfo(host, port)[0][-1]
    sock = socket.socket()
    sock.settimeout(timeout)
    sock.connect(addr)

    if scheme == "https":
        sock = ssl.wrap_socket(sock, server_hostname=host)

    # Build and send request
    request = "POST %s HTTP/1.0\r\nHost: %s\r\n" % (path, host)
    for k, v in headers.items():
        request += "%s: %s\r\n" % (k, v)
    request += "Content-Length: %d\r\n\r\n" % len(body_bytes)

    sock.write(request.encode())
    sock.write(body_bytes)

    # Read status line
    line = sock.readline()
    parts = line.split(None, 2)
    status = int(parts[1])

    # Skip response headers
    while True:
        line = sock.readline()
        if not line or line == b"\r\n":
            break

    # Read body
    body = sock.read()
    sock.close()

    return status, body


class OFREPClient:
    def __init__(self, base_url, bearer_token, timeout=10):
        self.base_url = base_url.rstrip("/")
        self.bearer_token = bearer_token
        self.timeout = timeout

    def evaluate_flag(self, key, context=None):
        if context is None:
            context = {}

        if "targetingKey" not in context:
            context["targetingKey"] = "badge-user"

        url = "%s/ofrep/v1/evaluate/flags/%s" % (self.base_url, key)
        body_bytes = json.dumps({"context": context}).encode()
        headers = {
            "Content-Type": "application/json",
        }
        if self.bearer_token:
            headers["Authorization"] = "Bearer %s" % self.bearer_token

        try:
            status, raw = _http_post(url, body_bytes, headers, self.timeout)
            try:
                response_payload = json.loads(raw.decode())
            except Exception:
                try:
                    response_payload = raw.decode()
                except Exception:
                    response_payload = str(raw)

            if status == 200:
                result = response_payload
                del raw
                value = result.get("value")
                return {
                    "key": result.get("key", key),
                    "value": value,
                    "value_type": _detect_type(value),
                    "reason": result.get("reason", "UNKNOWN"),
                    "variant": result.get("variant", ""),
                    "metadata": result.get("metadata", {}),
                    "request_context": context,
                    "http_status": status,
                    "ofrep_response": response_payload,
                    "error": None,
                }

            del raw
            if status == 404:
                return _error_result(
                    key,
                    "FLAG_NOT_FOUND",
                    "Flag not found",
                    context,
                    status,
                    response_payload,
                )
            if status == 401 or status == 403:
                return _error_result(
                    key,
                    "AUTH_ERROR",
                    "HTTP %d" % status,
                    context,
                    status,
                    response_payload,
                )
            if status == 429:
                return _error_result(
                    key,
                    "RATE_LIMITED",
                    "Too many requests",
                    context,
                    status,
                    response_payload,
                )

            return _error_result(
                key,
                "GENERAL",
                "HTTP %d" % status,
                context,
                status,
                response_payload,
            )

        except Exception as e:
            return _error_result(key, "NETWORK_ERROR", str(e), context)
        finally:
            gc.collect()


def _detect_type(value):
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int):
        return "integer"
    if isinstance(value, float):
        return "float"
    if isinstance(value, str):
        return "string"
    return "object"


def _error_result(key, code, message, context=None, http_status=None, payload=None):
    return {
        "key": key,
        "value": None,
        "value_type": None,
        "reason": "ERROR",
        "variant": "",
        "metadata": {},
        "request_context": context or {},
        "http_status": http_status,
        "ofrep_response": payload,
        "error": {"code": code, "message": message},
    }
