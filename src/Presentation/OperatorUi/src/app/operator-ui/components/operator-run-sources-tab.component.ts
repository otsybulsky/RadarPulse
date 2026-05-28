import { Component, Input } from '@angular/core';

import { ProductRunDetail } from '../../product/product-api.models';

@Component({
  selector: 'app-operator-run-sources-tab',
  templateUrl: './operator-run-sources-tab.component.html',
})
export class OperatorRunSourcesTabComponent {
  @Input({ required: true }) detail!: ProductRunDetail;
}
