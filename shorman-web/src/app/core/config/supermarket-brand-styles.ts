export interface SupermarketBrandStyle {
  name: string;
  backgroundColor: string;
  textColor: string;
  borderColor: string;
  fontWeight: number;
  textStrokeWidth: string;
}

export const SUPERMARKET_BRAND_STYLES: Record<string, SupermarketBrandStyle> = {
  all: {
    name: 'ALL',
    backgroundColor: '#E8F5E9',
    textColor: '#2E7D32',
    borderColor: '#2E7D32',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  rewe: {
    name: 'REWE',
    backgroundColor: '#CC0000',
    textColor: '#FFFFFF',
    borderColor: '#CC0000',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  aldi: {
    name: 'ALDI',
    backgroundColor: '#001E5A',
    textColor: '#FFFFFF',
    borderColor: '#001E5A',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  edeka: {
    name: 'EDEKA',
    backgroundColor: '#FFE500',
    textColor: '#00529B',
    borderColor: '#FFE500',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  penny: {
    name: 'PENNY',
    backgroundColor: '#CC0000',
    textColor: '#FFFFFF',
    borderColor: '#CC0000',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  lidl: {
    name: 'LIDL',
    backgroundColor: '#0050AA',
    textColor: '#FFD500',
    borderColor: '#0050AA',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  dm: {
    name: 'dm',
    backgroundColor: '#ffffffe7',
    textColor: '#003E91',
    borderColor: '#003E91',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  },
  rossmann: {
    name: 'ROSSMANN',
    backgroundColor: '#ffffffe7',
    textColor: '#C3002F',
    borderColor: '#C3002F',
    fontWeight: 900,
    textStrokeWidth: '0.5px'
  }
};

export const DEFAULT_SUPERMARKET_BRAND_STYLE: SupermarketBrandStyle = {
  name: '',
  backgroundColor: '#555555',
  textColor: '#FFFFFF',
  borderColor: '#555555',
  fontWeight: 900,
  textStrokeWidth: '0px'
};
