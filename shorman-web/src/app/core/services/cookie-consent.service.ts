import { Injectable } from '@angular/core';
import * as CookieConsent from 'vanilla-cookieconsent';

/**
 * Cookie consent (TDDDG §25 / GDPR). Only strictly necessary storage is used today.
 * The "analytics" category is a placeholder: load analytics (e.g. App Insights browser SDK)
 * ONLY inside onConsent/onChange when `CookieConsent.acceptedCategory('analytics')` is true.
 */
@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private initialized = false;

  init(): void {
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    void CookieConsent.run({
      guiOptions: {
        consentModal: { layout: 'box inline', position: 'bottom left', equalWeightButtons: true },
        preferencesModal: { layout: 'box', equalWeightButtons: true }
      },
      categories: {
        necessary: { enabled: true, readOnly: true },
        analytics: {}
      },
      language: {
        default: 'de',
        autoDetect: 'document',
        translations: {
          de: {
            consentModal: {
              title: 'Wir respektieren Ihre Privatsphäre',
              description:
                'Wir verwenden nur technisch notwendige Speicherung (z. B. Anmeldung, Warenkorb). Optionale Statistik-Dienste werden erst nach Ihrer Einwilligung aktiviert. Details in unserer <a href="/datenschutz">Datenschutzerklärung</a>.',
              acceptAllBtn: 'Alle akzeptieren',
              acceptNecessaryBtn: 'Nur notwendige',
              showPreferencesBtn: 'Einstellungen'
            },
            preferencesModal: {
              title: 'Cookie-Einstellungen',
              acceptAllBtn: 'Alle akzeptieren',
              acceptNecessaryBtn: 'Nur notwendige',
              savePreferencesBtn: 'Auswahl speichern',
              closeIconLabel: 'Schließen',
              sections: [
                {
                  title: 'Notwendige Speicherung',
                  description: 'Erforderlich für Anmeldung, Warenkorb und Bestellung. Kann nicht deaktiviert werden.',
                  linkedCategory: 'necessary'
                },
                {
                  title: 'Statistik',
                  description: 'Hilft uns, die Nutzung der Seite zu verstehen. Derzeit nicht aktiv.',
                  linkedCategory: 'analytics'
                }
              ]
            }
          },
          en: {
            consentModal: {
              title: 'We respect your privacy',
              description:
                'We only use technically necessary storage (e.g. login, cart). Optional analytics is enabled only with your consent. See our <a href="/datenschutz">privacy policy</a>.',
              acceptAllBtn: 'Accept all',
              acceptNecessaryBtn: 'Necessary only',
              showPreferencesBtn: 'Preferences'
            },
            preferencesModal: {
              title: 'Cookie preferences',
              acceptAllBtn: 'Accept all',
              acceptNecessaryBtn: 'Necessary only',
              savePreferencesBtn: 'Save selection',
              closeIconLabel: 'Close',
              sections: [
                {
                  title: 'Strictly necessary',
                  description: 'Required for login, cart and ordering. Cannot be disabled.',
                  linkedCategory: 'necessary'
                },
                {
                  title: 'Analytics',
                  description: 'Helps us understand site usage. Currently not active.',
                  linkedCategory: 'analytics'
                }
              ]
            }
          }
        }
      }
    });
  }

  openPreferences(): void {
    CookieConsent.showPreferences();
  }
}
