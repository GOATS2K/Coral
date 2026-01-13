import { useState, useEffect } from 'react';
import { View, ScrollView, Pressable, ActivityIndicator } from 'react-native';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { fetchRootDirectories, fetchDirectoriesInPath, fetchRegisterMusicLibrary, fetchMusicLibraries } from '@/lib/client/components';
import { Folder, ChevronRight, ArrowLeft, HardDrive } from 'lucide-react-native';
import { useColorScheme } from 'nativewind';
import type { MusicLibraryDto } from '@/lib/client/schemas';

interface LibrarySetupScreenProps {
  onComplete: () => void;
}

export default function LibrarySetupScreen({ onComplete }: LibrarySetupScreenProps) {
  const { colorScheme } = useColorScheme();
  const iconColor = colorScheme === 'dark' ? '#fff' : '#000';

  const [currentPath, setCurrentPath] = useState<string | null>(null);
  const [directories, setDirectories] = useState<string[]>([]);
  const [libraries, setLibraries] = useState<MusicLibraryDto[]>([]);
  const [pendingPaths, setPendingPaths] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Load root directories on mount
  useEffect(() => {
    loadRootDirectories();
    loadLibraries();
  }, []);

  const loadLibraries = async () => {
    try {
      const libs = await fetchMusicLibraries({});
      setLibraries(libs);
    } catch {
      // Silently fail - libraries will be empty
    }
  };

  const loadRootDirectories = async () => {
    setIsLoading(true);
    setError('');
    try {
      const roots = await fetchRootDirectories({});
      setDirectories(roots);
      setCurrentPath(null);
    } catch {
      setError('Failed to load directories');
    } finally {
      setIsLoading(false);
    }
  };

  const loadDirectories = async (path: string) => {
    setIsLoading(true);
    setError('');
    try {
      const dirs = await fetchDirectoriesInPath({ queryParams: { path } });
      setDirectories(dirs);
      setCurrentPath(path);
    } catch {
      setError('Failed to load directories');
    } finally {
      setIsLoading(false);
    }
  };

  const navigateUp = () => {
    if (currentPath === null) return;

    // At root? Go back to root list
    if (/^[A-Z]:$/i.test(currentPath) || currentPath === '/') {
      loadRootDirectories();
      return;
    }

    // Navigate to parent
    const sep = currentPath.includes('\\') ? '\\' : '/';
    const parts = currentPath.split(sep).filter(Boolean);
    parts.pop();

    if (parts.length === 0) {
      loadRootDirectories();
    } else {
      const parent = currentPath.includes('\\') ? parts.join('\\') : '/' + parts.join('/');
      loadDirectories(parent);
    }
  };

  const addLibrary = () => {
    if (!currentPath) return;
    // Don't add duplicates
    if (pendingPaths.includes(currentPath) || libraries.some(lib => lib.libraryPath === currentPath)) {
      return;
    }
    setPendingPaths(prev => [...prev, currentPath]);
  };

  const removePendingPath = (path: string) => {
    setPendingPaths(prev => prev.filter(p => p !== path));
  };

  const handleDone = async () => {
    if (pendingPaths.length === 0) {
      onComplete();
      return;
    }

    setIsLoading(true);
    setError('');
    try {
      for (const path of pendingPaths) {
        await fetchRegisterMusicLibrary({ queryParams: { path } });
      }
      onComplete();
    } catch {
      setError('Failed to add libraries');
    } finally {
      setIsLoading(false);
    }
  };

  const getName = (path: string) => {
    const sep = path.includes('\\') ? '\\' : '/';
    const parts = path.split(sep).filter(Boolean);
    return parts[parts.length - 1] || path;
  };

  return (
    <View className="flex-1 bg-background items-center justify-center px-4">
      <View className="max-w-md w-full">
        <Text variant="h2" className="mb-3 border-b-0">
          Add Your Music
        </Text>
        <Text variant="muted" className="mb-8">
          Select a folder containing your music library.
        </Text>

        {/* Existing libraries */}
        {libraries.length > 0 && (
          <View className="mb-4">
            <Text variant="muted" className="text-sm mb-2">Existing libraries:</Text>
            {libraries.map((lib) => (
              <View key={lib.id} className="flex-row items-center bg-muted rounded-lg p-3 mb-2">
                <Folder size={16} color={iconColor} />
                <Text className="flex-1 ml-2 font-mono text-sm" numberOfLines={1}>{lib.libraryPath}</Text>
              </View>
            ))}
          </View>
        )}

        {/* Pending libraries to add */}
        {pendingPaths.length > 0 && (
          <View className="mb-4">
            <Text variant="muted" className="text-sm mb-2">Libraries to add:</Text>
            {pendingPaths.map((path) => (
              <View key={path} className="flex-row items-center bg-muted rounded-lg p-3 mb-2">
                <Folder size={16} color={iconColor} />
                <Text className="flex-1 ml-2 font-mono text-sm" numberOfLines={1}>{path}</Text>
                <Pressable onPress={() => removePendingPath(path)} className="ml-2 p-1">
                  <Text className="text-destructive text-sm">Remove</Text>
                </Pressable>
              </View>
            ))}
          </View>
        )}

        {/* Directory browser */}
        <View className="flex-row items-center gap-2 mb-3">
          <Button variant="outline" size="icon" onPress={navigateUp} disabled={currentPath === null}>
            <ArrowLeft size={16} color={iconColor} />
          </Button>
          <View className="flex-1 bg-muted rounded-md px-3 py-2">
            <Text className="font-mono text-sm" numberOfLines={1}>
              {currentPath ?? 'Select a location'}
            </Text>
          </View>
        </View>

        {error && (
          <Text className="text-destructive text-sm mb-3">{error}</Text>
        )}

        {isLoading ? (
          <View className="py-8 items-center">
            <ActivityIndicator size="large" />
          </View>
        ) : (
          <ScrollView className="mb-4" style={{ maxHeight: 250 }}>
            {directories.map((dir) => (
              <Pressable
                key={dir}
                onPress={() => loadDirectories(dir)}
                className="flex-row items-center gap-3 py-3 px-2 border-b border-border active:bg-muted"
              >
                {currentPath === null ? <HardDrive size={18} color={iconColor} /> : <Folder size={18} color={iconColor} />}
                <Text className="flex-1">{currentPath === null ? dir : getName(dir)}</Text>
                <ChevronRight size={16} color={iconColor} />
              </Pressable>
            ))}
            {directories.length === 0 && (
              <Text variant="muted" className="text-center py-4">No subdirectories</Text>
            )}
          </ScrollView>
        )}

        {/* Actions */}
        <View className="gap-3">
          {currentPath && (
            <Button onPress={addLibrary} disabled={isLoading}>
              <Text>Add {currentPath}</Text>
            </Button>
          )}
          <Button
            onPress={handleDone}
            disabled={isLoading || (pendingPaths.length === 0 && libraries.length === 0)}
          >
            <Text>{isLoading ? 'Adding...' : 'Done'}</Text>
          </Button>
        </View>
      </View>
    </View>
  );
}
