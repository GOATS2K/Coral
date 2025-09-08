import pathlib
from essentia.standard import TensorflowPredictEffnetDiscogs  # type: ignore
from fastapi import FastAPI, Response
from pydantic import BaseModel, Field
from contextlib import asynccontextmanager

import requests

from recs.enums import Error
from recs.embeddings import extract_embeddings

from typing import Generic, TypeVar, TypedDict

import logging


# Configure basic logging

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)

logger = logging.getLogger(__name__)


class EssentiaModel(TypedDict):
    model_filename: str
    model_url: str
    instance: TensorflowPredictEffnetDiscogs


ml_models: dict[str, EssentiaModel] = {
    "discogs-effnet-track": {
        "model_filename": "discogs_track_embeddings-effnet-bs64-1.pb",
        "model_url": "https://essentia.upf.edu/models/feature-extractors/discogs-effnet/discogs_track_embeddings-effnet-bs64-1.pb",
        "instance": None,
    }
}


def ensure_model_present(model: EssentiaModel):
    if pathlib.Path(model["model_filename"]).exists():
        return
    logger.info(f"Downloading model: {model['model_filename']}")

    r = requests.get(model["model_url"])
    with open(model["model_filename"], "wb") as out:
        for chunk in r.iter_content(chunk_size=4096):
            out.write(chunk)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Loading Essentia models...")
    ensure_model_present(ml_models["discogs-effnet-track"])
    ml_models["discogs-effnet-track"]["instance"] = TensorflowPredictEffnetDiscogs(
        graphFilename="discogs_track_embeddings-effnet-bs64-1.pb",
        output="PartitionedCall:1",
    )
    logger.info("Essentia models loaded!")
    yield
    # Clean up the ML models and release the resources
    ml_models.clear()


T = TypeVar('T')

class BaseClass(BaseModel, Generic[T]):
    data: T | None

class BaseResponse(BaseClass[T], Generic[T]):
    success: bool
    error: Error | None

    class Config:  
        use_enum_values = True

class GetEmbeddingsRequest(BaseModel):
    file_path: str
    duration: int = Field(gt=60, lt=60 * 15, description="Track duration in seconds.")


class EmbeddingResponse(BaseModel):
    embeddings: list[float]


app = FastAPI(lifespan=lifespan)


@app.post("/api/embeddings")
def get_track_embeddings(request: GetEmbeddingsRequest, response: Response) -> BaseResponse[EmbeddingResponse]:
    model_instance = ml_models["discogs-effnet-track"]["instance"]
    embeddings, error = extract_embeddings(model_instance, request.file_path)
    if error:
        response.status_code = 500 if error == Error.UNKNOWN_PROCESSING_ERROR else 400
        return BaseResponse[EmbeddingResponse](error=error, success=False, data=EmbeddingResponse(embeddings=embeddings))
    return BaseResponse[EmbeddingResponse](error=None, success=True, data=EmbeddingResponse(embeddings=embeddings))
