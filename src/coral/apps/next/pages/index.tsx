import { Player } from 'app/features/player/Player'
import Head from 'next/head'

export default function Page() {
  return (
    <>
      <Head>
        <title>Home</title>
      </Head>
      <Player />
    </>
  )
}
