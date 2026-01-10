import { useState } from 'react';
import { View } from 'react-native';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/lib/hooks/use-auth';

export default function SetupScreen() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const { register, isLoading } = useAuth();

  const handleSetup = async () => {
    setError('');

    if (!username.trim()) {
      setError('Please enter a username');
      return;
    }

    if (!password) {
      setError('Please enter a password');
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    if (password.length < 8) {
      setError('Password must be at least 8 characters');
      return;
    }

    await register(username.trim(), password);
    // On success, the root layout will detect the user change and transition automatically
  };

  return (
    <View className="flex-1 bg-background items-center justify-center px-4">
      <View className="max-w-md w-full">
        <Text variant="h2" className="mb-3 border-b-0">
          Create your account
        </Text>
        <Text variant="muted" className="mb-8">
          Set up the first account for your Coral server.
        </Text>

        {error ? (
          <View className="bg-destructive/10 border border-destructive rounded-lg p-3 mb-5">
            <Text className="text-destructive text-sm">{error}</Text>
          </View>
        ) : null}

        <View className="mb-4 gap-2">
          <Label nativeID="username">Username</Label>
          <Input
            nativeID="username"
            placeholder="Choose a username"
            value={username}
            onChangeText={setUsername}
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="username"
            editable={!isLoading}
          />
          <Text variant="muted" className="text-xs">
            Letters, numbers, dashes, and underscores only
          </Text>
        </View>

        <View className="mb-4 gap-2">
          <Label nativeID="password">Password</Label>
          <Input
            nativeID="password"
            placeholder="Choose a password"
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="new-password"
            editable={!isLoading}
          />
        </View>

        <View className="mb-6 gap-2">
          <Label nativeID="confirmPassword">Confirm password</Label>
          <Input
            nativeID="confirmPassword"
            placeholder="Confirm your password"
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            secureTextEntry
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="new-password"
            editable={!isLoading}
          />
        </View>

        <Button onPress={handleSetup} disabled={isLoading} className="w-full">
          <Text>{isLoading ? 'Creating account...' : 'Create account'}</Text>
        </Button>
      </View>
    </View>
  );
}
