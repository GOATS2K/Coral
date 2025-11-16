import { RefreshCwIcon } from 'lucide-react-native';
import { Button } from '@/components/ui/button';
import { Icon } from '@/components/ui/icon';
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip';
import { Text } from '@/components/ui/text';
import { useRunIndexer } from '@/lib/client/components';
import { useToast } from '@/lib/hooks/use-toast';

export function RescanButton() {
  const { showToast } = useToast();
  const runIndexer = useRunIndexer({
    onSuccess: (data) => {
      showToast('Library scan started');
      console.info('[RescanButton] Scan started with requestId:', data.requestId);
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