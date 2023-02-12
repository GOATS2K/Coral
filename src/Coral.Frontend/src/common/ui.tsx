import { Center, Loader } from "@mantine/core";

export function CenteredLoader() {
  return (
    <Center style={{ height: "100vh" }}>
      <Loader></Loader>
    </Center>
  );
}
