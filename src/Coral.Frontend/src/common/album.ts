import { AlbumDto, SimpleAlbumDto, TrackDto } from "../client/schemas";
import { formatSecondsToDateString } from "../utils";

export const getAlbumArtists = (album: AlbumDto | SimpleAlbumDto) => {
  let combinationCharacter = " & ";
  if (album.artists?.length > 2) {
    combinationCharacter = ", ";
  }

  if (album.artists?.length >= 4) {
    return "Various Artists";
  }
  return album.artists?.map((a) => a.name).join(combinationCharacter);
};

export const getTrackArtists = (track: TrackDto) => {
  let combinationCharacter = " & ";
  const mainArtists = track.artists.filter((a) => a.role == "Main").map((a) => a.name);
  if (mainArtists.length > 2) {
    combinationCharacter = ", ";
  }
  const artistString = mainArtists.join(combinationCharacter);
  return artistString;
};

export const getAlbumGenre = (album: AlbumDto) => {
  const uniqueGenres = Array.from(new Set(album.tracks?.map((a) => a.genre?.name)));
  if (uniqueGenres.length >= 4) {
    return "Various Genres";
  }
  return uniqueGenres.join(", ");
};

export const getAlbumDuration = (album: AlbumDto) => {
  const totalDurationInSeconds = album.tracks
    ?.map((t) => t.durationInSeconds)
    .reduce((a, b) => a + b);
  return formatSecondsToDateString(totalDurationInSeconds);
};
