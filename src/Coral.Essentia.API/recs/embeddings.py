import logging
from essentia.standard import MonoLoader, TensorflowPredictEffnetDiscogs # type: ignore
import numpy as np

from recs.enums import Error

logger = logging.getLogger(__name__)

def extract_embeddings(model: TensorflowPredictEffnetDiscogs, file_path: str) -> tuple[list[float], Error | None]:
    loader = MonoLoader()

    try:
        loader.configure(filename=file_path, sampleRate=16000, resampleQuality=4)
        audio = loader()
        embeddings = model(audio)
        reduced_embeddings = np.mean(embeddings, axis=0)
        return reduced_embeddings.tolist(), None
    except Exception as e:
        empty_list: list[float] = []
        logger.error("Failed to process file", exc_info=e)
        exc_message: str = e.args[0]
        
        if "No such file or directory" in exc_message:
            return empty_list, Error.FILE_NOT_FOUND
        
        if "Could not find stream information" in exc_message:
            return empty_list, Error.UNPROCESSABLE_FILE
        
        if "Unsupported codec!" in exc_message:
            return empty_list, Error.UNSUPPORTED_CODEC
        
        return empty_list, Error.UNKNOWN_PROCESSING_ERROR
