# ---------------------------------------------------------------
# Build stage — install dependencies into an isolated layer
# so the final image only copies what's needed.
# ---------------------------------------------------------------
FROM python:3.11-slim AS builder

WORKDIR /build

# Install dependencies into a local directory (not system-wide)
# so we can copy only this folder into the final image.
COPY requirements.txt .
RUN pip install --upgrade pip \
    && pip install --prefix=/install --no-cache-dir -r requirements.txt


# ---------------------------------------------------------------
# Runtime stage — lean final image
# ---------------------------------------------------------------
FROM python:3.11-slim

WORKDIR /app

# Copy installed packages from the build stage
COPY --from=builder /install /usr/local

# Copy application source
COPY src/ ./src/

# Create a non-root user for security
RUN addgroup --system appgroup \
    && adduser --system --ingroup appgroup appuser \
    && chown -R appuser:appgroup /app

USER appuser

EXPOSE 8080

# PYTHONPATH tells Python where to find the `app` package.
ENV PYTHONPATH=/app/src

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8080"]
