export interface FavoriteRoutesResponse {
  routes: string[];
  last_updated: string;
}

export interface UserPreference {
  id: string;
  user_id: string;
  preference_key: string;
  preference_value: string;
  created_at: string;
  updated_at: string;
}

export interface SetFavoritesRequest {
  routes: string[];
}

export interface AddFavoriteRequest {
  route_name: string;
}

export interface SetPreferenceRequest {
  key: string;
  value: string;
}
