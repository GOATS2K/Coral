import { Player } from 'app/features/player/Player'
import { Stack } from 'expo-router'

export default function Screen() {
  return (
    <>
      <Stack.Screen
        options={{
          title: 'Home',
        }}
      />
      <Player />
    </>
  )
}
