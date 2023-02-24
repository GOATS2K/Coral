import React from "react";
import { ArtistDto } from "../../client/schemas";
import ArtistHero from "./ArtistHero";

type ArtistProps = {
  artist?: ArtistDto;
};

export default function Artist({ artist }: ArtistProps) {
  return (
    <div>
      <ArtistHero artist={artist}></ArtistHero>
    </div>
  );
}
