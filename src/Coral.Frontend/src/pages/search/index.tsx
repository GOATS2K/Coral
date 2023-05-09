import { TextInput } from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { useState } from "react";
import Search from "../../components/search/Search";
import { useSearchStore } from "../../store";
import styles from "../../styles/Search.module.css";

export default function SearchPage() {
  const storeQueryString = useSearchStore((state) => state.query);
  const [query, setQuery] = useState(storeQueryString);
  const [debounced] = useDebouncedValue(query, 500);

  if (storeQueryString !== query) {
    useSearchStore.getState().setQueryString(debounced);
  }

  return (
    <div className={styles.wrapper}>
      <TextInput
        placeholder="Search..."
        value={query}
        variant="filled"
        size="md"
        style={{ marginBottom: "1em" }}
        onChange={(event) => {
          setQuery(event.currentTarget.value);
        }}
      />
      <Search />
    </div>
  );
}
