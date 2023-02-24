import { AlbumDto, SimpleAlbumDto, TrackDto } from "../client/schemas";
import { formatSecondsToDateString } from "../utils";

export const getAlbumArtists = (album: AlbumDto | SimpleAlbumDto) => {
  if (album.artists?.length >= 4) {
    return "Various Artists";
  }
  return album.artists?.map((a) => a.name).join(", ");
};

export const getTrackArtists = (track: TrackDto) => {
    let combinationCharacter = " & ";
    if (track.artists?.length > 2) {
      combinationCharacter = ", "
    }
    const mainArtists = track.artists.filter(a => a.role == "Main")
    .map(a => a.name);
    const artistString = mainArtists.join(combinationCharacter);
    return artistString;
}

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
