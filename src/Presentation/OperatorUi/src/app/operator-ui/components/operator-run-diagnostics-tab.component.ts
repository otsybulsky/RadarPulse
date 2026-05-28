import { Component, Input } from '@angular/core';

import { ProductRunDetail } from '../../product/product-api.models';

@Component({
  selector: 'app-operator-run-diagnostics-tab',
  templateUrl: './operator-run-diagnostics-tab.component.html',
})
export class OperatorRunDiagnosticsTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;
}
