// Vercel serverless entry: every path is rewritten here (see vercel.json) and handled by the Express app.
import { createApp } from "../src/app.js";

const app = createApp();
export default app;
