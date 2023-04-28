import { TextInput } from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { useState } from "react";
import Search from "../../components/search/Search";
import styles from "../../styles/Search.module.css";

export default function SearchPage() {
  const [searchString, setSearchString] = useState("");
  const [debounced] = useDebouncedValue(searchString, 500);

  return (
    <div className={styles.wrapper}>
      <TextInput
        placeholder="Search..."
        value={searchString}
        variant="filled"
        size="md"
        style={{ marginBottom: "1em" }}
        onChange={(event) => setSearchString(event.currentTarget.value)}
      />
      <Search searchString={debounced} />
    </div>
  );
}
