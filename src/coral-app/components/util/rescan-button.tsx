import { RefreshCwIcon } from 'lucide-react-native';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip';
import { Text } from '@/components/ui/text';
import { useRunIndexer } from '@/lib/client/components';
import { useToast } from '@/lib/hooks/use-toast';
import { useSetAtom } from 'jotai';
import { updateScanProgressAtom } from '@/lib/signalr/signalr-atoms';

export function RescanButton() {
  const { showToast } = useToast();
  const updateScanProgress = useSetAtom(updateScanProgressAtom);

  const runIndexer = useRunIndexer({
    onSuccess: (data) => {
      showToast('Library scan started');
      console.info('[RescanButton] Scan initiated:', data);

      // Initialize scan progress for each library
      data.scans.forEach((scan) => {
        updateScanProgress({
          requestId: scan.requestId,
          libraryName: scan.libraryName,
          expectedTracks: 0, // Will be updated when backend reports actual count
          tracksAdded: 0,
          tracksUpdated: 0,
          tracksDeleted: 0,
          embeddingsCompleted: 0,
        });
      });
    },
    onError: (error) => {
      console.error('[RescanButton] Failed to start scan:', error);
      showToast('Failed to start library scan');
    },
  });

  const handleRescan = () => {
    runIndexer.mutate({});
  };

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          onPress={handleRescan}
          size="icon"
          variant="ghost"
          className="rounded-full"
          disabled={runIndexer.isPending}
        >
          <Icon
            as={RefreshCwIcon}
            className={runIndexer.isPending ? "size-5 animate-spin" : "size-5"}
          />
        </Button>
      </TooltipTrigger>
      <TooltipContent>
        <Text>Rescan Library</Text>
      </TooltipContent>
    </Tooltip>
  );
}