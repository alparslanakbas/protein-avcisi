import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { config } from './app/app.config.server';

// main.ts'teki ile aynı gerekçe — SSR'ın kendi bootstrap giriş noktası
// ayrı olduğu için burada da kayıt edilmesi gerekiyor.
registerLocaleData(localeTr);

const bootstrap = (context: BootstrapContext) => bootstrapApplication(App, config, context);

export default bootstrap;
