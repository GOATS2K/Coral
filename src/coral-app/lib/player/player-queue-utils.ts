import type { SimpleTrackDto } from '@/lib/client/schemas';
import type { PlayerState } from '@/lib/state';
import { fetchRecommendationsForTrack } from '@/lib/client/components';

type SetState = (update: PlayerState | ((prev: PlayerState) => PlayerState)) => void;

export function addToQueue(setState: SetState, track: SimpleTrackDto) {
  setState(prev => ({
    ...prev,
    queue: [...prev.queue, track],
    originalQueue: prev.originalQueue ? [...prev.originalQueue, track] : null,
  }));
}

export function addMultipleToQueue(setState: SetState, tracks: SimpleTrackDto[]) {
  setState(prev => ({
    ...prev,
    queue: [...prev.queue, ...tracks],
    originalQueue: prev.originalQueue ? [...prev.originalQueue, ...tracks] : null,
  }));
}

export function removeFromQueue(setState: SetState, index: number) {
  setState(prev => {
    const newQueue = [...prev.queue];
    const removedTrack = newQueue[index];
    newQueue.splice(index, 1);

    // Also remove from originalQueue if it exists
    let newOriginalQueue = prev.originalQueue;
    if (newOriginalQueue && removedTrack) {
      newOriginalQueue = newOriginalQueue.filter(t => t.id !== removedTrack.id);
    }

    // Adjust currentIndex if necessary
    let newCurrentIndex = prev.currentIndex;
    if (index < prev.currentIndex) {
      newCurrentIndex = prev.currentIndex - 1;
    } else if (index === prev.currentIndex) {
      // If removing current track, don't change index (next track moves into position)
      // But if it was the last track, go to previous
      if (index >= newQueue.length && newQueue.length > 0) {
        newCurrentIndex = newQueue.length - 1;
      }
    }

    return {
      ...prev,
      queue: newQueue,
      currentIndex: Math.max(0, newCurrentIndex),
      currentTrack: newQueue[newCurrentIndex] || prev.currentTrack,
      originalQueue: newOriginalQueue,
    };
  });
}

export function reorderQueue(setState: SetState, fromIndex: number, toIndex: number) {
  setState(prev => {
    const newQueue = [...prev.queue];
    const [removed] = newQueue.splice(fromIndex, 1);
    newQueue.splice(toIndex, 0, removed);

    // Adjust currentIndex if the current track was moved
    let newCurrentIndex = prev.currentIndex;
    if (fromIndex === prev.currentIndex) {
      newCurrentIndex = toIndex;
    } else if (fromIndex < prev.currentIndex && toIndex >= prev.currentIndex) {
      newCurrentIndex = prev.currentIndex - 1;
    } else if (fromIndex > prev.currentIndex && toIndex <= prev.currentIndex) {
      newCurrentIndex = prev.currentIndex + 1;
    }

    return {
      ...prev,
      queue: newQueue,
      currentIndex: newCurrentIndex,
    };
  });
}

export function shuffleQueue(setState: SetState) {
  setState(prev => {
    const newShuffleState = !prev.isShuffled;

    if (!newShuffleState) {
      // Turning shuffle off - restore original queue
      if (!prev.originalQueue || !prev.currentTrack) {
        return { ...prev, isShuffled: false, originalQueue: null };
      }

      // Find current track in original queue
      const newIndex = prev.originalQueue.findIndex(t => t.id === prev.currentTrack!.id);
      if (newIndex === -1) {
        // Current track not found in original queue, keep shuffled state
        return { ...prev, isShuffled: false, originalQueue: null };
      }

      return {
        ...prev,
        queue: prev.originalQueue,
        currentIndex: newIndex,
        isShuffled: false,
        originalQueue: null,
      };
    }

    // Turning shuffle on - save original queue and shuffle
    const currentTrack = prev.queue[prev.currentIndex];

    // Get all tracks except current
    const otherTracks = [
      ...prev.queue.slice(0, prev.currentIndex),
      ...prev.queue.slice(prev.currentIndex + 1)
    ];

    // Fisher-Yates shuffle on all other tracks
    const shuffled = [...otherTracks];
    for (let i = shuffled.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }

    // Current track at top, shuffled tracks after
    const newQueue = [currentTrack, ...shuffled];

    return {
      ...prev,
      queue: newQueue,
      currentIndex: 0,
      isShuffled: true,
      originalQueue: prev.queue,
    };
  });
}

export function cycleRepeat(setState: SetState) {
  setState(prev => {
    const modes: Array<'off' | 'all' | 'one'> = ['off', 'all', 'one'];
    const currentIndex = modes.indexOf(prev.repeat);
    const nextMode = modes[(currentIndex + 1) % modes.length];
    return { ...prev, repeat: nextMode };
  });
}

export async function findSimilarAndAddToQueue(
  trackId: string,
  setState: SetState,
  showToast: (message: string) => void
) {
  try {
    const recommendations = await fetchRecommendationsForTrack({ pathParams: { trackId } });
    // Skip first track as it's the track we're getting recommendations for
    const tracksToAdd = recommendations.slice(1);
    addMultipleToQueue(setState, tracksToAdd);
    showToast(`Added ${tracksToAdd.length} similar songs to queue`);
  } catch (err) {
    console.error('Failed to fetch recommendations:', err);
    showToast('Failed to fetch recommendations');
  }
}
