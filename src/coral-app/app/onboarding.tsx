import { useState } from 'react';
import { View } from 'react-native';
import { useRouter } from 'expo-router';
import { Config } from '@/lib/config';
import { resetBaseUrl } from '@/lib/client/fetcher';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export default function OnboardingScreen() {
  const [serverUrl, setServerUrl] = useState('http://localhost:5031');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const router = useRouter();

  const handleContinue = async () => {
    setError('');

    if (!serverUrl.trim()) {
      setError('Please enter a server URL');
      return;
    }

    // Basic URL validation
    try {
      new URL(serverUrl);
    } catch {
      setError('Please enter a valid URL (e.g., http://localhost:5031)');
      return;
    }

    setIsLoading(true);

    try {
      await Config.setBackendUrl(serverUrl);
      await Config.completeFirstRun();

      // Reset the cached base URL so it picks up the new one
      await resetBaseUrl();

      // Reload the page to re-check configuration
      if (typeof window !== 'undefined') {
        window.location.reload();
      } else {
        // Fallback for non-web platforms
        router.replace('/(tabs)');
      }
    } catch {
      setError('Failed to save configuration');
      setIsLoading(false);
    }
  };

  return (
    <View className="flex-1 bg-background items-center justify-center px-4">
        <View className="max-w-md w-full">
          <Text variant="h2" className="mb-3 border-b-0">
            Welcome to Coral
          </Text>
          <Text variant="muted" className="mb-8">
            To get started, enter the URL of your Coral server.
          </Text>

          {error ? (
            <View className="bg-destructive/10 border border-destructive rounded-lg p-3 mb-5">
              <Text className="text-destructive text-sm">{error}</Text>
            </View>
          ) : null}

          <View className="mb-6 gap-2">
            <Label nativeID="serverUrl">Server URL</Label>
            <Input
              nativeID="serverUrl"
              placeholder="http://localhost:5031"
              value={serverUrl}
              onChangeText={setServerUrl}
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="off"
              editable={!isLoading}
              className="font-mono"
            />
            <Text variant="muted" className="text-xs">
              e.g. https://coral.example.com
            </Text>
          </View>

          <Button onPress={handleContinue} disabled={isLoading} className="w-full">
            <Text>{isLoading ? 'Saving...' : 'Continue'}</Text>
          </Button>
        </View>
    </View>
  );
}
