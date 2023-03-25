import { Center, Loader } from "@mantine/core";

export function CenteredLoader() {
  return (
    <Center style={{ height: "100dvh" }}>
      <Loader />
    </Center>
  );
}
