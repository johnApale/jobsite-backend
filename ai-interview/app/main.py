from fastapi import FastAPI

app = FastAPI(title="Jobsite AI Interview Service")


@app.get("/health")
async def health():
    return {"status": "healthy"}
