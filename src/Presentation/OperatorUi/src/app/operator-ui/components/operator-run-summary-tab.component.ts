import { Component, Input } from '@angular/core';

import { ProductEnumValue, ProductRunDetail } from '../../product/product-api.models';
import { formatProductEnumLabel, EnumKind } from '../operator-ui-format';

@Component({
  selector: 'app-operator-run-summary-tab',
  templateUrl: './operator-run-summary-tab.component.html',
})
export class OperatorRunSummaryTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;

  protected enumLabel(value: ProductEnumValue, kind: EnumKind): string {
    return formatProductEnumLabel(value, kind);
  }

  protected combinedWarnings(): readonly string[] {
    return [...this.detail.operatorSummary.warnings, ...this.detail.configuration.warnings];
  }
}
