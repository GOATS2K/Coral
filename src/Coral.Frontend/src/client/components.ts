/**
 * Generated by @openapi-codegen
 *
 * @version v1
 */
import * as reactQuery from "@tanstack/react-query";
import { useContext, Context } from "./context";
import type * as Fetcher from "./fetcher";
import { fetch } from "./fetcher";
import type * as Schemas from "./schemas";

export type FileFromLibraryPathParams = {
  /**
   * @format int32
   */
  trackId: number;
};

export type FileFromLibraryError = Fetcher.ErrorWrapper<undefined>;

export type FileFromLibraryVariables = {
  pathParams: FileFromLibraryPathParams;
} & Context["fetcherOptions"];

export const fetchFileFromLibrary = (
  variables: FileFromLibraryVariables,
  signal?: AbortSignal
) =>
  fetch<
    undefined,
    FileFromLibraryError,
    undefined,
    {},
    {},
    FileFromLibraryPathParams
  >({
    url: "/api/Repository/tracks/{trackId}/original",
    method: "get",
    ...variables,
    signal,
  });

export const useFileFromLibrary = <TData = undefined>(
  variables: FileFromLibraryVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<undefined, FileFromLibraryError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<undefined, FileFromLibraryError, TData>(
    queryKeyFn({
      path: "/api/Repository/tracks/{trackId}/original",
      operationId: "fileFromLibrary",
      variables,
    }),
    ({ signal }) =>
      fetchFileFromLibrary({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type TranscodeTrackPathParams = {
  /**
   * @format int32
   */
  trackId: number;
};

export type TranscodeTrackQueryParams = {
  /**
   * @format int32
   */
  bitrate?: number;
};

export type TranscodeTrackError = Fetcher.ErrorWrapper<undefined>;

export type TranscodeTrackVariables = {
  pathParams: TranscodeTrackPathParams;
  queryParams?: TranscodeTrackQueryParams;
} & Context["fetcherOptions"];

export const fetchTranscodeTrack = (
  variables: TranscodeTrackVariables,
  signal?: AbortSignal
) =>
  fetch<
    Schemas.StreamDto,
    TranscodeTrackError,
    undefined,
    {},
    TranscodeTrackQueryParams,
    TranscodeTrackPathParams
  >({
    url: "/api/Repository/tracks/{trackId}/transcode",
    method: "get",
    ...variables,
    signal,
  });

export const useTranscodeTrack = <TData = Schemas.StreamDto>(
  variables: TranscodeTrackVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<Schemas.StreamDto, TranscodeTrackError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<Schemas.StreamDto, TranscodeTrackError, TData>(
    queryKeyFn({
      path: "/api/Repository/tracks/{trackId}/transcode",
      operationId: "transcodeTrack",
      variables,
    }),
    ({ signal }) =>
      fetchTranscodeTrack({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type StreamTrackPathParams = {
  /**
   * @format int32
   */
  trackId: number;
};

export type StreamTrackQueryParams = {
  /**
   * @format int32
   * @default 192
   */
  bitrate?: number;
  /**
   * @default true
   */
  transcodeTrack?: boolean;
};

export type StreamTrackError = Fetcher.ErrorWrapper<undefined>;

export type StreamTrackVariables = {
  pathParams: StreamTrackPathParams;
  queryParams?: StreamTrackQueryParams;
} & Context["fetcherOptions"];

export const fetchStreamTrack = (
  variables: StreamTrackVariables,
  signal?: AbortSignal
) =>
  fetch<
    Schemas.StreamDto,
    StreamTrackError,
    undefined,
    {},
    StreamTrackQueryParams,
    StreamTrackPathParams
  >({
    url: "/api/Repository/tracks/{trackId}/stream",
    method: "get",
    ...variables,
    signal,
  });

export const useStreamTrack = <TData = Schemas.StreamDto>(
  variables: StreamTrackVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<Schemas.StreamDto, StreamTrackError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<Schemas.StreamDto, StreamTrackError, TData>(
    queryKeyFn({
      path: "/api/Repository/tracks/{trackId}/stream",
      operationId: "streamTrack",
      variables,
    }),
    ({ signal }) =>
      fetchStreamTrack({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type TrackArtworkPathParams = {
  /**
   * @format int32
   */
  trackId: number;
};

export type TrackArtworkError = Fetcher.ErrorWrapper<undefined>;

export type TrackArtworkVariables = {
  pathParams: TrackArtworkPathParams;
} & Context["fetcherOptions"];

export const fetchTrackArtwork = (
  variables: TrackArtworkVariables,
  signal?: AbortSignal
) =>
  fetch<
    undefined,
    TrackArtworkError,
    undefined,
    {},
    {},
    TrackArtworkPathParams
  >({
    url: "/api/Repository/tracks/{trackId}/artwork",
    method: "get",
    ...variables,
    signal,
  });

export const useTrackArtwork = <TData = undefined>(
  variables: TrackArtworkVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<undefined, TrackArtworkError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<undefined, TrackArtworkError, TData>(
    queryKeyFn({
      path: "/api/Repository/tracks/{trackId}/artwork",
      operationId: "trackArtwork",
      variables,
    }),
    ({ signal }) =>
      fetchTrackArtwork({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type AlbumArtworkPathParams = {
  /**
   * @format int32
   */
  albumId: number;
};

export type AlbumArtworkError = Fetcher.ErrorWrapper<undefined>;

export type AlbumArtworkVariables = {
  pathParams: AlbumArtworkPathParams;
} & Context["fetcherOptions"];

export const fetchAlbumArtwork = (
  variables: AlbumArtworkVariables,
  signal?: AbortSignal
) =>
  fetch<
    undefined,
    AlbumArtworkError,
    undefined,
    {},
    {},
    AlbumArtworkPathParams
  >({
    url: "/api/Repository/albums/{albumId}/artwork",
    method: "get",
    ...variables,
    signal,
  });

export const useAlbumArtwork = <TData = undefined>(
  variables: AlbumArtworkVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<undefined, AlbumArtworkError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<undefined, AlbumArtworkError, TData>(
    queryKeyFn({
      path: "/api/Repository/albums/{albumId}/artwork",
      operationId: "albumArtwork",
      variables,
    }),
    ({ signal }) =>
      fetchAlbumArtwork({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type TracksError = Fetcher.ErrorWrapper<undefined>;

export type TracksResponse = Schemas.TrackDto[];

export type TracksVariables = Context["fetcherOptions"];

export const fetchTracks = (variables: TracksVariables, signal?: AbortSignal) =>
  fetch<TracksResponse, TracksError, undefined, {}, {}, {}>({
    url: "/api/Repository/tracks",
    method: "get",
    ...variables,
    signal,
  });

export const useTracks = <TData = TracksResponse>(
  variables: TracksVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<TracksResponse, TracksError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<TracksResponse, TracksError, TData>(
    queryKeyFn({
      path: "/api/Repository/tracks",
      operationId: "tracks",
      variables,
    }),
    ({ signal }) => fetchTracks({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type AlbumsError = Fetcher.ErrorWrapper<undefined>;

export type AlbumsResponse = Schemas.SimpleAlbumDto[];

export type AlbumsVariables = Context["fetcherOptions"];

export const fetchAlbums = (variables: AlbumsVariables, signal?: AbortSignal) =>
  fetch<AlbumsResponse, AlbumsError, undefined, {}, {}, {}>({
    url: "/api/Repository/albums",
    method: "get",
    ...variables,
    signal,
  });

export const useAlbums = <TData = AlbumsResponse>(
  variables: AlbumsVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<AlbumsResponse, AlbumsError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<AlbumsResponse, AlbumsError, TData>(
    queryKeyFn({
      path: "/api/Repository/albums",
      operationId: "albums",
      variables,
    }),
    ({ signal }) => fetchAlbums({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type AlbumPathParams = {
  /**
   * @format int32
   */
  albumId: number;
};

export type AlbumError = Fetcher.ErrorWrapper<undefined>;

export type AlbumVariables = {
  pathParams: AlbumPathParams;
} & Context["fetcherOptions"];

export const fetchAlbum = (variables: AlbumVariables, signal?: AbortSignal) =>
  fetch<Schemas.AlbumDto, AlbumError, undefined, {}, {}, AlbumPathParams>({
    url: "/api/Repository/albums/{albumId}",
    method: "get",
    ...variables,
    signal,
  });

export const useAlbum = <TData = Schemas.AlbumDto>(
  variables: AlbumVariables,
  options?: Omit<
    reactQuery.UseQueryOptions<Schemas.AlbumDto, AlbumError, TData>,
    "queryKey" | "queryFn"
  >
) => {
  const { fetcherOptions, queryOptions, queryKeyFn } = useContext(options);
  return reactQuery.useQuery<Schemas.AlbumDto, AlbumError, TData>(
    queryKeyFn({
      path: "/api/Repository/albums/{albumId}",
      operationId: "album",
      variables,
    }),
    ({ signal }) => fetchAlbum({ ...fetcherOptions, ...variables }, signal),
    {
      ...options,
      ...queryOptions,
    }
  );
};

export type QueryOperation =
  | {
      path: "/api/Repository/tracks/{trackId}/original";
      operationId: "fileFromLibrary";
      variables: FileFromLibraryVariables;
    }
  | {
      path: "/api/Repository/tracks/{trackId}/transcode";
      operationId: "transcodeTrack";
      variables: TranscodeTrackVariables;
    }
  | {
      path: "/api/Repository/tracks/{trackId}/stream";
      operationId: "streamTrack";
      variables: StreamTrackVariables;
    }
  | {
      path: "/api/Repository/tracks/{trackId}/artwork";
      operationId: "trackArtwork";
      variables: TrackArtworkVariables;
    }
  | {
      path: "/api/Repository/albums/{albumId}/artwork";
      operationId: "albumArtwork";
      variables: AlbumArtworkVariables;
    }
  | {
      path: "/api/Repository/tracks";
      operationId: "tracks";
      variables: TracksVariables;
    }
  | {
      path: "/api/Repository/albums";
      operationId: "albums";
      variables: AlbumsVariables;
    }
  | {
      path: "/api/Repository/albums/{albumId}";
      operationId: "album";
      variables: AlbumVariables;
    };