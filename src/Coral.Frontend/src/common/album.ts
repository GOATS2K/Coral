import { AlbumDto } from "../client/schemas";
import { formatSecondsToDateString } from "../utils";

export const getAlbumArtists = (album: AlbumDto) => {
  if (album.artists?.length >= 4) {
    return "Various Artists";
  }
  return album.artists?.map((a) => a.name).join(", ");
};

export const getAlbumGenre = (album: AlbumDto) => {
  let uniqueGenres = Array.from(
    new Set(album.tracks?.map((a) => a.genre?.name))
  );
  if (uniqueGenres.length >= 4) {
    return "Various Genres";
  }
  return uniqueGenres.join(", ");
};

export const getAlbumDuration = (album: AlbumDto) => {
  let totalDurationInSeconds = album.tracks
    ?.map((t) => t.durationInSeconds)
    .reduce((a, b) => a + b);
  return formatSecondsToDateString(totalDurationInSeconds);
};
