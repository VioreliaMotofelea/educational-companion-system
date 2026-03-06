import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from api.exceptions import BackendError
from api.routes import router

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Educational Companion AI Service",
    description="AI recommender microservice for the Educational Companion System",
    version="1.0",
)

app.include_router(router)


@app.exception_handler(BackendError)
def backend_error_handler(request: Request, exc: BackendError):
    """Return consistent JSON error for backend failures (like backend's ExceptionMiddleware)."""
    logger.warning(
        "Backend error: status=%s detail=%s path=%s",
        exc.status_code,
        exc.detail,
        request.url.path,
    )
    return JSONResponse(
        status_code=exc.status_code,
        content={"detail": exc.detail},
    )


@app.exception_handler(ValidationError)
def validation_error_handler(request: Request, exc: ValidationError):
    """Invalid data from backend or internal model; do not leak details. Skip request validation (422)."""
    if isinstance(exc, RequestValidationError):
        raise exc  # Let FastAPI return 422 for invalid request body/params
    logger.warning("Validation error: %s", exc)
    return JSONResponse(
        status_code=502,
        content={"detail": "Invalid response from backend or internal validation failed."},
    )


@app.exception_handler(Exception)
def unhandled_exception_handler(request: Request, exc: Exception):
    """Catch-all: recommendation logic bugs, KeyError, etc. Return generic 500, log the real error."""
    logger.exception("Unhandled exception")
    return JSONResponse(
        status_code=500,
        content={"detail": "An unexpected internal server error occurred."},
    )


@app.get("/")
def root():
    return {"service": "Educational Companion AI", "status": "running"}