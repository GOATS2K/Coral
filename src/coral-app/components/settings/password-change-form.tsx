import { View } from 'react-native';
import { useState } from 'react';

import { Text } from '@/components/ui/text';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useChangePassword } from '@/lib/client/components';
import { useToast } from '@/lib/hooks/use-toast';

interface PasswordChangeFormProps {
  onSuccess?: () => void;
  onCancel?: () => void;
}

export function PasswordChangeForm({ onSuccess, onCancel }: PasswordChangeFormProps) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  const changePasswordMutation = useChangePassword();
  const { showToast } = useToast();

  const handleSubmit = async () => {
    if (!currentPassword || !newPassword || !confirmPassword) {
      showToast('Please fill in all fields');
      return;
    }

    if (newPassword !== confirmPassword) {
      showToast('New passwords do not match');
      return;
    }

    if (newPassword.length < 6) {
      showToast('Password must be at least 6 characters');
      return;
    }

    try {
      await changePasswordMutation.mutateAsync({
        body: { currentPassword, newPassword },
      });
      showToast('Password changed successfully');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      onSuccess?.();
    } catch (error: any) {
      const message = error?.error || 'Current password is incorrect';
      showToast(message);
    }
  };

  const handleCancel = () => {
    setCurrentPassword('');
    setNewPassword('');
    setConfirmPassword('');
    onCancel?.();
  };

  return (
    <View className="gap-4">
      <Input
        placeholder="Current password"
        secureTextEntry
        value={currentPassword}
        onChangeText={setCurrentPassword}
        autoCapitalize="none"
        autoComplete="current-password"
      />
      <Input
        placeholder="New password"
        secureTextEntry
        value={newPassword}
        onChangeText={setNewPassword}
        autoCapitalize="none"
        autoComplete="new-password"
      />
      <Input
        placeholder="Confirm new password"
        secureTextEntry
        value={confirmPassword}
        onChangeText={setConfirmPassword}
        autoCapitalize="none"
        autoComplete="new-password"
      />
      <View className="flex-row gap-2 justify-end mt-2">
        {onCancel && (
          <Button variant="ghost" onPress={handleCancel}>
            <Text>Cancel</Text>
          </Button>
        )}
        <Button
          onPress={handleSubmit}
          disabled={changePasswordMutation.isPending}
        >
          <Text>{changePasswordMutation.isPending ? 'Saving...' : 'Change Password'}</Text>
        </Button>
      </View>
    </View>
  );
}
