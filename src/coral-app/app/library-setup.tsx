import { useState, useEffect } from 'react';
import { View, ScrollView, Pressable, ActivityIndicator, TextInput } from 'react-native';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import {
  fetchRootDirectories,
  fetchDirectoriesInPath,
  fetchRegisterMusicLibrary,
  fetchMusicLibraries,
  fetchGetSystemInfo,
  fetchGetConfiguration,
  fetchConfigureInference
} from '@/lib/client/components';
import { AlertTriangle, Folder, ChevronRight, ArrowLeft, HardDrive, Minus, Plus } from 'lucide-react-native';
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

  // Inference settings
  const [cpuCores, setCpuCores] = useState<number | null>(null);
  const [instances, setInstances] = useState(4);

  // Load root directories and system info on mount
  useEffect(() => {
    loadInitialData();
  }, []);

  const loadInitialData = async () => {
    setIsLoading(true);
    setError('');
    try {
      const [roots, libs, systemInfo, config] = await Promise.all([
        fetchRootDirectories({}),
        fetchMusicLibraries({}).catch(() => []),
        fetchGetSystemInfo({}),
        fetchGetConfiguration({})
      ]);
      setDirectories(roots);
      setLibraries(libs);
      setCpuCores(systemInfo.cpuCores);
      const defaultThreads = Math.max(1, Math.floor(systemInfo.cpuCores / 2));
      // Only use saved config if libraries exist (not first-time setup)
      const savedThreads = libs.length > 0 ? config.inference?.maxConcurrentInstances : null;
      setInstances(savedThreads ?? defaultThreads);
      setCurrentPath(null);
    } catch {
      setError('Failed to load data');
    } finally {
      setIsLoading(false);
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
    if (pendingPaths.length === 0 && libraries.length === 0) {
      return;
    }

    setIsLoading(true);
    setError('');
    try {
      // Save inference settings first
      await fetchConfigureInference({ body: { maxConcurrentInstances: instances } });

      // Then register libraries
      for (const path of pendingPaths) {
        await fetchRegisterMusicLibrary({ queryParams: { path } });
      }
      onComplete();
    } catch {
      setError('Failed to save settings');
    } finally {
      setIsLoading(false);
    }
  };

  const getName = (path: string) => {
    const sep = path.includes('\\') ? '\\' : '/';
    const parts = path.split(sep).filter(Boolean);
    return parts[parts.length - 1] || path;
  };

  const incrementInstances = () => {
    if (cpuCores && instances < cpuCores) {
      setInstances(prev => prev + 1);
    }
  };

  const decrementInstances = () => {
    if (instances > 1) {
      setInstances(prev => prev - 1);
    }
  };

  return (
    <ScrollView className="flex-1 bg-background" contentContainerClassName="items-center justify-center px-4 py-8">
      <View className="max-w-md w-full">
        <Text variant="h2" className="mb-3 border-b-0">
          Setup Coral
        </Text>
        <Text variant="muted" className="mb-8">
          Configure your music library and server settings.
        </Text>

        {/* Sonic Analysis Settings */}
        {cpuCores && (
          <View className="mb-8">
            <Text className="font-medium mb-2">Sonic Analysis</Text>
            <Text variant="muted" className="text-sm mb-4">
              Coral scans your music library for songs that are similar to each other. The analyzer is slow and resource intensive.
            </Text>
            <Text variant="muted" className="text-sm mb-2">
              How many CPU threads would you like to allocate to sonic analysis?
            </Text>
            <Text variant="muted" className="text-sm mb-4">
              You can expect roughly 3-4 seconds computation time per track per thread and ~{instances * 500}MB memory usage while analysis is active.
            </Text>
            <View className="flex-row items-center justify-between">
              <Text>CPU Threads ({cpuCores} max)</Text>
              <View className="flex-row items-center h-10">
                <TextInput
                  value={String(instances)}
                  onChangeText={(text) => {
                    const val = parseInt(text, 10);
                    if (!isNaN(val) && val >= 1 && val <= cpuCores) {
                      setInstances(val);
                    } else if (text === '') {
                      setInstances(1);
                    }
                  }}
                  keyboardType="number-pad"
                  selectTextOnFocus
                  className="w-12 h-10 border border-border rounded-l-md bg-background text-foreground text-lg font-medium text-center"
                />
                <View className="h-10 border border-l-0 border-border rounded-r-md overflow-hidden">
                  <Pressable
                    onPress={incrementInstances}
                    disabled={instances >= cpuCores}
                    className="w-7 h-5 items-center justify-center active:bg-muted disabled:opacity-50"
                  >
                    <Plus size={12} color={iconColor} />
                  </Pressable>
                  <View className="h-px bg-border" />
                  <Pressable
                    onPress={decrementInstances}
                    disabled={instances <= 1}
                    className="w-7 h-5 items-center justify-center active:bg-muted disabled:opacity-50"
                  >
                    <Minus size={12} color={iconColor} />
                  </Pressable>
                </View>
              </View>
            </View>
            {instances > cpuCores / 2 && (
              <Alert icon={AlertTriangle} variant="destructive" className="mt-4">
                <AlertDescription>
                  Allocating more than 50% of CPU threads may result in decreased indexing performance and slow overall system performance.
                </AlertDescription>
              </Alert>
            )}
          </View>
        )}

        <View className="h-px bg-border mb-6" />

        {/* Music Library Section */}
        <Text className="font-medium mb-3">Music Library</Text>

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
          <View className="mb-4" style={{ maxHeight: 200 }}>
            <ScrollView nestedScrollEnabled>
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
          </View>
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
            <Text>{isLoading ? 'Saving...' : 'Done'}</Text>
          </Button>
        </View>
      </View>
    </ScrollView>
  );
}
