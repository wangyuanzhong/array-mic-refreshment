import './styles/tokens.css';
import './styles/components.css';
import { startRouter } from './router';

const root = document.getElementById('app');
if (!root) {
  throw new Error('Missing #app mount point');
}

startRouter(root);
