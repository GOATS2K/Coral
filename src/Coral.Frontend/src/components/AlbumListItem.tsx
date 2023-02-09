import React, { useState } from "react";
import { Anchor, Image, Text } from "@mantine/core";
import { SimpleAlbumDto } from "../client/schemas";
import styles from "../styles/AlbumList.module.css";
import { getAlbumArtists } from "../common/album";
import getConfig from "next/config";
import Link from "next/link";

type AlbumListItemProps = {
  album: SimpleAlbumDto;
};

export default function AlbumListItem({ album }: AlbumListItemProps) {
  const baseUrl = getConfig().publicRuntimeConfig.apiBaseUrl;
  return (
    <div className={styles.item}>
      <Link href={`/albums/${album.id}`}>
        <Image
          className={styles.image}
          withPlaceholder
          width={200}
          height={200}
          src={`${baseUrl}/api/repository/albums/${album.id}/artwork`}
        ></Image>
      </Link>
      <div>
        <Link className={styles.link} passHref href={`/albums/${album.id}`}>
          <Anchor
            className={styles.link}
            component="a"
            lineClamp={2}
            fz={"md"}
            fw={"bold"}
          >
            {album.name}
          </Anchor>
        </Link>
        <Text lineClamp={2} fz={"sm"} fw={"light"}>
          {getAlbumArtists(album)}
        </Text>
      </div>
    </div>
  );
}
