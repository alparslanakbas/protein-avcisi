import { registerLocaleData } from '@angular/common';
import localeTr from '@angular/common/locales/tr';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// DatePipe'ın 'tr' locale'i ("d MMMM yyyy" -> "14 Ağustos 2026" gibi) tanıması için gerekli
// — kayıt edilmezse pipe hata fırlatıp o bileşenin render'ını tamamen durduruyordu.
registerLocaleData(localeTr);

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
