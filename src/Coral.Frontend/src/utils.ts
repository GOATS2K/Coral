import dayjs from "dayjs";
import duration from "dayjs/plugin/duration";

dayjs.extend(duration);

export function formatSecondsToMinutes(value: number): string {
  if (value > 3600) {
    return dayjs.duration(value, "seconds").format("H:mm:ss");  
  }
  return dayjs.duration(value, "seconds").format("mm:ss");
}

export function formatSecondsToSingleMinutes(value: number): string {
  if (value > 3600) {
    return dayjs.duration(value, "seconds").format("H:mm:ss");  
  }
  return dayjs.duration(value, "seconds").format("m:ss");
}

export function formatSecondsToDateString(value: number): string {
  if (value > 3600) {
    return dayjs.duration(value, "seconds").format("H [h] mm [min] ss [sec]");  
  }
  return dayjs.duration(value, "seconds").format("mm [min] ss [sec]");
}
