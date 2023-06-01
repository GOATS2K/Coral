import { useEffect, useState } from "react";
import { useDirectoriesInPath, useRegisterMusicLibrary } from "../../client/components";
import { Modal, Button, Select } from "@mantine/core";
import styles from "../../styles/Onboarding.module.css";

type PathPickerModalProps = {
  opened: boolean;
  close: () => void;
};

export default function PathPicker({ opened, close }: PathPickerModalProps) {
  const [search, setSearch] = useState("");
  const [pathList, setPathList] = useState<string[]>([]);
  const [selectedPath, setSelectedPath] = useState<string | null>("");
  const { data } = useDirectoriesInPath(
    {
      queryParams: {
        path: search,
      },
    },
    {
      enabled: search != "",
    }
  );

  const registerMusicLibrary = useRegisterMusicLibrary({});

  useEffect(() => {
    if (data?.length !== 0 && data != null) {
      setPathList(data);
    }
  }, [data]);

  return (
    <Modal opened={opened} onClose={close} centered withCloseButton={false}>
      <div className={styles.modal}>
        <Select
          label="Select your music library folder"
          placeholder="Music library path"
          nothingFound="Try another path name"
          data={pathList}
          searchable
          value={selectedPath}
          onChange={(value) => {
            if (value != null) {
              setSearch(value);
              setSelectedPath(value);
            }
          }}
          searchValue={search}
          onSearchChange={setSearch}
        />
        <Button
          fullWidth
          onClick={() => {
            if (selectedPath != null) {
              registerMusicLibrary.mutate({ queryParams: { path: selectedPath } });
              close();
            }
          }}
        >
          Select folder
        </Button>
      </div>
    </Modal>
  );
}
