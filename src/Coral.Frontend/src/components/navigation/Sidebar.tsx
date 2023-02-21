import { ThemeContext } from "@emotion/react";
import { Button, Title } from "@mantine/core";
import { IconAlbum, IconSearch, IconVinyl } from "@tabler/icons";
import Link from "next/link";
import React from "react";
import styles from "../../styles/Layout.module.css";

export default function Sidebar() {
  const links = [
    { icon: <IconVinyl />, name: "Albums", link: "/albums" },
    { icon: <IconSearch />, name: "Search", link: "/search" },
  ];

  const buttons = links.map((link) => (
    <Link href={link.link} className={"link"} key={link.name}>
      <Button
        fullWidth
        size={"md"}
        key={link.name}
        variant={"subtle"}
        leftIcon={link.icon}
        styles={(theme) => ({
          root: {
            marginBottom: theme.spacing.xs,
          },
          inner: {
            justifyContent: "flex-start",
          },
        })}
      >
        {link.name}
      </Button>
    </Link>
  ));

  return (
    <div className={styles.sidebar}>
      <Title order={1} style={{ textAlign: "center" }} mb={"sm"}>
        Coral
      </Title>
      {buttons}
    </div>
  );
}
