import { getApp, getApps, initializeApp } from 'firebase/app';
import { getAuth } from 'firebase/auth';
import { getFirestore } from 'firebase/firestore';

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? 'AIzaSyBUAxvW09jUV9980IObSBej_9-tlWzhKTg',
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? 'tradingengine-a8c0e.firebaseapp.com',
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? 'tradingengine-a8c0e',
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET ?? 'tradingengine-a8c0e.firebasestorage.app',
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID ?? '1089366218413',
  appId: import.meta.env.VITE_FIREBASE_APP_ID ?? '1:1089366218413:web:e89f078d4c39bb34165824',
  measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID ?? 'G-M05EJME2KX',
};

const app = getApps().length > 0 ? getApp() : initializeApp(firebaseConfig);

export const auth = getAuth(app);
export const db = getFirestore(app);
