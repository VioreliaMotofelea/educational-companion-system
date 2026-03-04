from fastapi import FastAPI
from api.routes import router

app = FastAPI(
    title="Educational Companion AI Service",
    description="AI recommender microservice for the Educational Companion System",
    version="1.0"
)

app.include_router(router)

@app.get("/")
def root():
    return {"service": "Educational Companion AI", "status": "running"}