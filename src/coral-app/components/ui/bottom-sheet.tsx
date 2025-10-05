import BottomSheet, {
  BottomSheetBackdrop,
  BottomSheetBackdropProps,
  BottomSheetModal,
  BottomSheetView,
} from '@gorhom/bottom-sheet';
import { useAtomValue } from 'jotai';
import React, { forwardRef, useMemo } from 'react';
import { Pressable, View } from 'react-native';
import { themeAtom } from '@/lib/state';
import { Text } from '@/components/ui/text';

interface MenuBottomSheetProps {
  children: React.ReactNode;
}

export const MenuBottomSheet = forwardRef<BottomSheetModal, MenuBottomSheetProps>(
  ({ children }, ref) => {
    const theme = useAtomValue(themeAtom);
    const snapPoints = useMemo(() => ['50%'], []);

    const renderBackdrop = React.useCallback(
      (props: BottomSheetBackdropProps) => (
        <BottomSheetBackdrop
          {...props}
          disappearsOnIndex={-1}
          appearsOnIndex={0}
          opacity={0.5}
        />
      ),
      []
    );

    return (
      <BottomSheetModal
        ref={ref}
        snapPoints={snapPoints}
        backdropComponent={renderBackdrop}
        enablePanDownToClose
        backgroundStyle={{
          backgroundColor: theme === 'dark' ? '#18181b' : '#ffffff',
        }}
        handleIndicatorStyle={{
          backgroundColor: theme === 'dark' ? '#52525b' : '#d4d4d8',
        }}
      >
        <BottomSheetView style={{ flex: 1, paddingHorizontal: 16, paddingBottom: 16 }}>
          {children}
        </BottomSheetView>
      </BottomSheetModal>
    );
  }
);

MenuBottomSheet.displayName = 'MenuBottomSheet';

// Bottom Sheet Menu Item
interface BottomSheetMenuItemProps {
  onPress?: () => void;
  children: React.ReactNode;
  disabled?: boolean;
}

export function BottomSheetMenuItem({ onPress, children, disabled }: BottomSheetMenuItemProps) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      className="flex-row items-center gap-3 py-3 px-2 active:bg-muted/50 rounded-md"
    >
      {children}
    </Pressable>
  );
}

// Bottom Sheet Menu Separator
export function BottomSheetMenuSeparator() {
  return <View className="h-px bg-border my-2" />;
}
