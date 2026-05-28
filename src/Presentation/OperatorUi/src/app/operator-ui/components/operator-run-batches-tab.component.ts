import { Component, Input } from '@angular/core';

import { ProductRunDetail } from '../../product/product-api.models';

@Component({
  selector: 'app-operator-run-batches-tab',
  templateUrl: './operator-run-batches-tab.component.html',
})
export class OperatorRunBatchesTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;
}
