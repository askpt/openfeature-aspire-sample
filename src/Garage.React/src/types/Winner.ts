export interface Winner {
  year: number;
  manufacturer: string;
  model: string;
  engine: string;
  class: string;
  drivers: string[];
  isOwned: boolean;
  image?: string;
}

export enum FilterType {
  All = "All",
  Owned = "Owned",
  NotOwned = "NotOwned",
}
