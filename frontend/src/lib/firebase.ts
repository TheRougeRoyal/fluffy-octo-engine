import { getApp, getApps, initializeApp } from 'firebase/app';
import { getAuth } from 'firebase/auth';
import { getFirestore } from 'firebase/firestore';

function getRequiredFirebaseEnv(name: string): string {
  const value = import.meta.env[name];
  if (!value) {
    throw new Error(
      `Missing required environment variable ${name} — copy .env.example to .env and fill in your Firebase project's config`,
    );
  }

  return value;
}

const firebaseConfig = {
  apiKey: getRequiredFirebaseEnv('VITE_FIREBASE_API_KEY'),
  authDomain: getRequiredFirebaseEnv('VITE_FIREBASE_AUTH_DOMAIN'),
  projectId: getRequiredFirebaseEnv('VITE_FIREBASE_PROJECT_ID'),
  storageBucket: getRequiredFirebaseEnv('VITE_FIREBASE_STORAGE_BUCKET'),
  messagingSenderId: getRequiredFirebaseEnv('VITE_FIREBASE_MESSAGING_SENDER_ID'),
  appId: getRequiredFirebaseEnv('VITE_FIREBASE_APP_ID'),
  measurementId: getRequiredFirebaseEnv('VITE_FIREBASE_MEASUREMENT_ID'),
};

const app = getApps().length > 0 ? getApp() : initializeApp(firebaseConfig);

export const auth = getAuth(app);
export const db = getFirestore(app);
