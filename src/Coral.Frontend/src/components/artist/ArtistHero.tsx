import { Image, Text, Title } from "@mantine/core";
import React from "react";
import { ArtistDto } from "../../client/schemas";
import styles from "../../styles/Artist.module.css";

type ArtistHeroProps = {
  artist?: ArtistDto;
};

export default function ArtistHero({ artist }: ArtistHeroProps) {
  const avatar = "https://sample-genie.com/wp-content/uploads/2019/04/SATL.jpg";
  return (
    <div className={styles.heroWrapper}>
      <Image
        className={styles.heroImage}
        src={avatar}
        alt={""}
        height={150}
        width={150}
        radius={100}
        withPlaceholder
      ></Image>
      <div>
        <Title className={styles.heroTitle} order={1}>
          {artist?.name}
        </Title>
        <div className="attributes">
          <Text className="attribute">{artist?.releases.length} releases</Text>
        </div>
      </div>
    </div>
  );
}
