import { View } from 'react-native';
import { Disc } from 'lucide-react-native';

interface MissingAlbumCoverProps {
  size?: number;
}

export function MissingAlbumCover({ size = 24 }: MissingAlbumCoverProps) {
  return (
    <View className="w-full h-full items-center justify-center bg-muted">
      <Disc size={size} className="text-muted-foreground opacity-40" />
    </View>
  );
}
