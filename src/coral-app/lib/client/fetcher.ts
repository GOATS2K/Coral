import axios, { AxiosError } from 'axios';
import { Context } from './context';
import { Config } from '../config';

// Base URL - initialized synchronously with default, then updated from config
export let baseUrl = 'http://localhost:5031';

// Initialize base URL from config
let isInitialized = false;
const initializeBaseUrl = async () => {
  if (!isInitialized) {
    baseUrl = await Config.getBackendUrl();
    isInitialized = true;
  }
};

// Get base URL (async, ensures initialization)
export const getBaseUrl = async (): Promise<string> => {
  await initializeBaseUrl();
  return baseUrl;
};

// Reset cached URL (useful when user changes backend URL in settings)
export const resetBaseUrl = async () => {
  baseUrl = await Config.getBackendUrl();
};

export type ErrorWrapper<TError> = TError | { status: 'unknown'; payload: string };

export type FetcherOptions<TBody, THeaders, TQueryParams, TPathParams> = {
  url: string;
  method: string;
  body?: TBody;
  headers?: THeaders;
  queryParams?: TQueryParams;
  pathParams?: TPathParams;
  signal?: AbortSignal;
} & Context['fetcherOptions'];

/**
 * A simplified URL resolver that only replaces path parameters (e.g., /users/{id}).
 */
const resolveUrl = (url: string, pathParams: Record<string, any> = {}) => {
  return url.replace(/\{\w*\}/g, (key) => pathParams[key.slice(1, -1)] ?? '');
};

/**
 * An Axios-based fetcher optimized for JSON APIs.
 * It assumes all successful and error responses are in JSON format.
 */
export async function fetch<
  TData,
  TError,
  TBody extends {} | FormData | undefined | null,
  THeaders extends {},
  TQueryParams extends {},
  TPathParams extends {},
>({
  url,
  method,
  body,
  headers,
  pathParams,
  queryParams,
  signal,
  ...rest // Capture any other options from the context
}: FetcherOptions<TBody, THeaders, TQueryParams, TPathParams>): Promise<TData> {
  try {
    const baseUrl = await getBaseUrl();
    const response = await axios<TData>({
      url: `${baseUrl}${resolveUrl(url, pathParams)}`,
      method,
      headers: { ...headers },
      params: queryParams,
      data: body,
      signal,
      ...rest, // Spread any extra options from the context
    });

    // Axios automatically parses the JSON response, so we can return it directly.
    return response.data;
  } catch (error) {
    if (axios.isAxiosError<TError>(error)) {
      // The server responded with an error (e.g., 4xx, 5xx).
      if (error.response) {
        // The error payload is in `error.response.data`, already parsed by Axios.
        throw error.response.data;
      } else {
        // A network error occurred (no response from server).
        const errorWrapper: ErrorWrapper<TError> = {
          status: 'unknown',
          payload: `Network Error: ${error.message}`,
        };
        throw errorWrapper;
      }
    }

    // A non-Axios, unexpected error occurred.
    const errorWrapper: ErrorWrapper<TError> = {
      status: 'unknown',
      payload:
        error instanceof Error
          ? `Unexpected Error: ${error.message}`
          : 'An unexpected error occurred',
    };
    throw errorWrapper;
  }
}
