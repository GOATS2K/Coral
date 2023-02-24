import { Button, Title } from "@mantine/core";
import { IconSearch, IconUsers, IconVinyl } from "@tabler/icons";
import Link from "next/link";
import React from "react";
import styles from "../../styles/Layout.module.css";

export default function Sidebar() {
  const links = [
    { icon: <IconSearch />, name: "Search", link: "/search" },
    { icon: <IconUsers />, name: "Artists", link: "/artists" },
    { icon: <IconVinyl />, name: "Albums", link: "/albums" },
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
      {/* <Title order={1} style={{ textAlign: "center" }} mb={"sm"}>
        Coral
      </Title> */}
      {buttons}
    </div>
  );
}
