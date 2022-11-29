import dayjs from "dayjs";
import duration from "dayjs/plugin/duration";

dayjs.extend(duration);

export function formatSecondsToMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format("mm:ss");
}

export function formatSecondsToSingleMinutes(value: number): string {
  return dayjs.duration(value, "seconds").format("m:ss");
}