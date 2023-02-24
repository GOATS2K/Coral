import { TextInput } from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import React, { useState } from "react";
import Search from "../../components/search/Search";
import styles from "../../styles/Search.module.css";

export default function SearchPage() {
  const [searchString, setSearchString] = useState("");
  const [debounced] = useDebouncedValue(searchString, 250);

  return (
    <div className={styles.wrapper}>
      <TextInput
        placeholder={"Search..."}
        value={searchString}
        variant="filled"
        size={"md"}
        style={{ marginBottom: "1em" }}
        onChange={(event) => setSearchString(event.currentTarget.value)}
      ></TextInput>
      <Search searchString={debounced}></Search>
    </div>
  );
}
