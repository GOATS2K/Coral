import { Image, Text, Title } from "@mantine/core";
import React, { useEffect, useState } from "react";
import { ArtistDto } from "../../client/schemas";
import styles from "../../styles/Artist.module.css";
import { FastAverageColor, FastAverageColorResult } from "fast-average-color";
import { ThemeContext } from "@emotion/react";

type ArtistHeroProps = {
  artist?: ArtistDto;
};

export default function ArtistHero({ artist }: ArtistHeroProps) {
  const avatar =
    "https://localhost:7031/api/artwork/170e820b-3d2c-43be-8328-dcfa4df49fc5";

  return (
    <div
      className={styles.heroBackground}
      style={{
        backgroundImage: `url(${avatar})`,
      }}
    >
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
          <Title className={styles.heroTitle} color={"white"} order={1}>
            {artist?.name}
          </Title>
          <div className="attributes">
            <Text className="attribute">
              {artist?.releases.length} releases
            </Text>
          </div>
        </div>
      </div>
    </div>
  );
}
