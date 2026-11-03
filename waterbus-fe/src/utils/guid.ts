const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** True for a backend id (every id of the API is a GUID). */
export const isGuid = (value: string): boolean => GUID.test(value)
