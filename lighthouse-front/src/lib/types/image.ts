// Docker image as exposed by the admin image-management API.
// Mirrors the backend `ImageDto` (camelCase JSON, 1:1).
export interface Image {
  id: string;
  repoTags: string[];
  size: number;
  created: string;
  dangling: boolean;
  inUseBy: string[];
  isSelf: boolean;
}

// Result of POST /api/images/prune.
export interface PruneImagesResult {
  imagesDeleted: string[];
  spaceReclaimed: number;
}
