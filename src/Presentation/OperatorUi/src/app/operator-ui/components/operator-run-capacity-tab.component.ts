import { Component, Input } from '@angular/core';

import { ProductRunDetail } from '../../product/product-api.models';

@Component({
  selector: 'app-operator-run-capacity-tab',
  templateUrl: './operator-run-capacity-tab.component.html',
})
export class OperatorRunCapacityTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;
}
