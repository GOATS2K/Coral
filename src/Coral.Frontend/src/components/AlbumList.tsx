import React from "react";
import AlbumListItem from "./AlbumListItem";
import styles from "../styles/AlbumList.module.css";

export default function AlbumList() {
  return (
    <div className={styles.wrapper}>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
      <AlbumListItem></AlbumListItem>
    </div>
  );
}
