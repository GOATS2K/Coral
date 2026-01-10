import { View, ActivityIndicator } from 'react-native';
import { useState } from 'react';
import { useAtomValue } from 'jotai';
import { UserIcon, KeyIcon, MonitorIcon, SmartphoneIcon, GlobeIcon } from 'lucide-react-native';

import { Text } from '@/components/ui/text';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { SettingsSection } from '@/components/settings/settings-section';
import { SettingItem } from '@/components/settings/setting-item';
import { PasswordChangeForm } from '@/components/settings/password-change-form';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { currentUserAtom, themeAtom } from '@/lib/state';
import { useGetDevices, useDeleteDevice } from '@/lib/client/components';
import type { DeviceDto } from '@/lib/client/schemas';

export function AccountSettings() {
  const currentUser = useAtomValue(currentUserAtom);
  const theme = useAtomValue(themeAtom);
  const [showPasswordForm, setShowPasswordForm] = useState(false);

  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  return (
    <View className="gap-6">
      {/* User Info Card */}
      <View className="bg-card rounded-lg p-4">
        <View className="flex-row items-center gap-4">
          <View className="w-16 h-16 rounded-full bg-accent items-center justify-center">
            <Icon as={UserIcon} className="size-8" color={iconColor} />
          </View>
          <View className="flex-1">
            <Text className="text-xl font-semibold">{currentUser?.username}</Text>
            <Text className="text-muted-foreground">{currentUser?.role}</Text>
          </View>
        </View>
      </View>

      {/* Security Section */}
      <SettingsSection title="Security">
        <SettingItem
          icon={KeyIcon}
          label="Change Password"
          onPress={() => setShowPasswordForm(!showPasswordForm)}
          showChevron={!showPasswordForm}
        />
      </SettingsSection>

      {/* Password Change Form */}
      {showPasswordForm && (
        <View className="bg-card rounded-lg p-4 -mt-4">
          <PasswordChangeForm
            onSuccess={() => setShowPasswordForm(false)}
            onCancel={() => setShowPasswordForm(false)}
          />
        </View>
      )}

      {/* Sessions Section */}
      <SettingsSection title="Sessions">
        <SessionsList />
      </SettingsSection>
    </View>
  );
}

function SessionsList() {
  const theme = useAtomValue(themeAtom);
  const iconColor = theme === 'dark' ? '#a1a1aa' : '#71717a';

  const { data: devices, isLoading, refetch } = useGetDevices({});
  const deleteDevice = useDeleteDevice();

  const [confirmDevice, setConfirmDevice] = useState<DeviceDto | null>(null);

  const handleDeauthorize = async () => {
    if (!confirmDevice) return;
    await deleteDevice.mutateAsync({ pathParams: { id: confirmDevice.id } });
    setConfirmDevice(null);
    refetch();
  };

  const getDeviceIcon = (type: DeviceDto['type']) => {
    switch (type) {
      case 'Native':
        return SmartphoneIcon;
      case 'Electron':
        return MonitorIcon;
      case 'Web':
      default:
        return GlobeIcon;
    }
  };

  const formatLastSeen = (dateString: string) => {
    // Ensure UTC interpretation - append 'Z' if no timezone specified
    const utcString = dateString.endsWith('Z') || dateString.includes('+') || dateString.includes('-', 10)
      ? dateString
      : dateString + 'Z';
    const date = new Date(utcString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  };

  if (isLoading) {
    return (
      <View className="p-4 items-center">
        <ActivityIndicator />
      </View>
    );
  }

  if (!devices || devices.length === 0) {
    return (
      <View className="p-4">
        <Text className="text-muted-foreground">No active sessions</Text>
      </View>
    );
  }

  return (
    <>
      <View>
        {devices.map((device) => (
          <View
            key={device.id}
            className="p-3 flex-row items-center border-b border-border last:border-b-0"
          >
            <Icon as={getDeviceIcon(device.type)} className="size-5 mr-3" color={iconColor} />
            <View className="flex-1">
              <View className="flex-row items-center gap-2">
                <Text className="font-medium">{device.name}</Text>
                {device.isCurrent && (
                  <View className="bg-primary/20 px-2 py-0.5 rounded">
                    <Text className="text-xs text-primary">Current</Text>
                  </View>
                )}
              </View>
              <Text className="text-xs text-muted-foreground">
                {device.type} · {device.os} · {formatLastSeen(device.lastSeenAt)}
              </Text>
            </View>
            {!device.isCurrent && (
              <Button
                variant="destructive"
                size="sm"
                onPress={() => setConfirmDevice(device)}
              >
                <Text className="text-white text-sm font-medium">Deauthorize</Text>
              </Button>
            )}
          </View>
        ))}
      </View>

      {/* Confirmation Dialog */}
      <Dialog open={confirmDevice !== null} onOpenChange={(open) => !open && setConfirmDevice(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Deauthorize Session</DialogTitle>
            <DialogDescription>
              Would you like to log {confirmDevice?.name} out of Coral?
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="outline"
              onPress={() => setConfirmDevice(null)}
            >
              <Text>Cancel</Text>
            </Button>
            <Button
              variant="destructive"
              onPress={handleDeauthorize}
              disabled={deleteDevice.isPending}
            >
              <Text className="text-white font-medium">
                {deleteDevice.isPending ? 'Deauthorizing...' : 'Deauthorize'}
              </Text>
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
