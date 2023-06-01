import { useMusicLibraries } from "../../client/components";
import PathPicker from "./PathPicker";
import { Text } from "@mantine/core";
import { useDisclosure } from "@mantine/hooks";

export default function Onboarding() {
  const { data } = useMusicLibraries({});
  const [opened, { close }] = useDisclosure(true);
  if (data?.length === 0) {
    return <PathPicker opened={opened} close={close} />;
  }
  return (
    <div>
      <Text>Hello world!</Text>
    </div>
  );
}
