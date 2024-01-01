import { Button, Paragraph, YStack } from '@coral/ui'
import { ChevronLeft } from '@tamagui/lucide-icons'
import React from 'react'
import { useParams } from 'solito/navigation'
import { useLink } from 'solito/navigation'


export function UserDetailScreen() {
  const params = useParams<{ id: string }>()
  const link = useLink({
    href: '/',
  })

  return (
    <YStack f={1} jc="center" ai="center" space>
      <Paragraph ta="center" fow="700">{`User ID: ${params.id}`}</Paragraph>
      <Button {...link} icon={ChevronLeft}>
        Go Home
      </Button>
    </YStack>
  )
}
