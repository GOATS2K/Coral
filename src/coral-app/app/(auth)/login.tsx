import { useState } from 'react';
import { View } from 'react-native';
import { Button } from '@/components/ui/button';
import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useAuth } from '@/lib/hooks/use-auth';

export default function LoginScreen() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { login, isLoading } = useAuth();

  const handleLogin = async () => {
    setError('');

    if (!username.trim()) {
      setError('Please enter your username');
      return;
    }

    if (!password) {
      setError('Please enter your password');
      return;
    }

    const success = await login(username.trim(), password);
    if (!success) {
      setError('Invalid username or password');
    }
    // On success, the root layout will detect the user change and transition automatically
  };

  return (
    <View className="flex-1 bg-background items-center justify-center px-4">
      <View className="max-w-md w-full">
        <Text variant="h2" className="mb-3 border-b-0">
          Welcome back
        </Text>
        <Text variant="muted" className="mb-8">
          Sign in to your Coral account
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
            placeholder="Enter your username"
            value={username}
            onChangeText={setUsername}
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="username"
            editable={!isLoading}
          />
        </View>

        <View className="mb-6 gap-2">
          <Label nativeID="password">Password</Label>
          <Input
            nativeID="password"
            placeholder="Enter your password"
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="password"
            editable={!isLoading}
          />
        </View>

        <Button onPress={handleLogin} disabled={isLoading} className="w-full">
          <Text>{isLoading ? 'Signing in...' : 'Sign in'}</Text>
        </Button>
      </View>
    </View>
  );
}
