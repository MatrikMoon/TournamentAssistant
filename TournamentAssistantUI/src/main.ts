import App from './App.svelte'
import { log } from './stores';

const app = new App({
  target: document.getElementById('app')
});

const oldConsole = (window as any).console;

(window as any).console = {
  log: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'log' }, ...x]);
    oldConsole.log(logParameter);
  },

  debug: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'debug' }, ...x]);
    oldConsole.debug(logParameter);
  },

  info: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'info' }, ...x]);
    oldConsole.info(logParameter);
  },

  warn: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'warn' }, ...x]);
    oldConsole.warn(logParameter);
  },

  error: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'error' }, ...x]);
    oldConsole.error(logParameter);
  },

  success: function (logParameter: any) {
    log.update(x => [{ message: logParameter, type: 'success' }, ...x]);
    oldConsole.log(logParameter);
  }
}

export default app;