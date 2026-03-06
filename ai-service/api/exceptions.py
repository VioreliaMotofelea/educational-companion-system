"""
API-layer exceptions for consistent error handling.

Similar to the backend's ExceptionMiddleware: all backend-related failures
are converted to BackendError so the global handler can return a uniform
JSON response (detail + status code) without leaking internals or crashing.
"""


class BackendError(Exception):
    """Raised when a call to the backend fails (HTTP error, timeout, or connection)."""

    def __init__(self, status_code: int, detail: str):
        self.status_code = status_code
        self.detail = detail
        super().__init__(detail)
