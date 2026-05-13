export const environment = {
  production: false,
  apiUrl: 'https://shorman-api-g0fwefe9cmezgeam.westeurope-01.azurewebsites.net/api', //'http://localhost:5000/api', //
  auth0: {
    domain: 'dev-a3b5v471sfb0dbt2.us.auth0.com',
    clientId: 'BJs2kBk5n7x7OAw50QWGMfOyiCzmmNXL',
    audience: 'https://api.shorman'
  },
  useMockProducts: false,
  useMockAuth: false,
  useMockAddresses: false,
  useMockOrders: false,
  defaultLanguage: 'en',
  supportedLanguages: ['en', 'de']
};
