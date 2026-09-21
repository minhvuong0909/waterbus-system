import axios from "axios";

export const api = axios.create({
  baseURL: "https://your-api.com",
  timeout: 10000,
});
